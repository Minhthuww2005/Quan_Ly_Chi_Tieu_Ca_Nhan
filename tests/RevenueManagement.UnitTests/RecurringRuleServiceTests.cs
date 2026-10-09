using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Recurring;
using RevenueManagement.Api.Features.Revenues;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.Api.Options;
using RevenueManagement.UnitTests.TestSupport;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RecurringRuleServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 7);

    private static readonly TestTimeProvider Clock = new(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero));

    private static (RecurringRuleService Service, RevenueJobQueue Queue) CreateService(AppDbContext db, RevenueJobQueue? queue = null)
    {
        queue ??= new RevenueJobQueue(Microsoft.Extensions.Options.Options.Create(new WorkerOptions()));
        return (new RecurringRuleService(db, new RevenueValidator(db, Clock), queue, Clock), queue);
    }

    private static CreateRecurringRuleRequest NewRequest(long walletId = TestDb.WalletId, DateOnly? start = null)
        => new()
        {
            WalletId = walletId,
            CategoryId = walletId == TestDb.WalletId ? TestDb.CategoryId : null,
            Amount = 2_000_000m,
            Frequency = RecurrenceFrequency.Monthly,
            DayOfMonth = 10,
            StartDate = start ?? Today
        };

    [Fact]
    public async Task Create_sets_income_kind_creator_and_first_run()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();

        var result = await CreateService(db).Service.CreateAsync(TestDb.OwnerId, NewRequest(), Ct);

        Assert.Equal(RecurringResultStatus.Ok, result.Status);
        var rule = await db.RecurringRules.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(TransactionKind.Income, rule.Kind);
        Assert.Equal(TestDb.OwnerId, rule.CreatedByUserId);
        Assert.Equal(new DateOnly(2026, 10, 10), rule.NextRunOn);
        Assert.Equal("Asia/Ho_Chi_Minh", rule.TimeZoneId);
        Assert.Equal(TestDb.GroupId, result.Value!.GroupId);
    }

    [Fact]
    public async Task Create_with_past_start_does_not_backfill()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var request = NewRequest(start: new DateOnly(2026, 1, 10));

        var result = await CreateService(db).Service.CreateAsync(TestDb.OwnerId, request, Ct);

        Assert.Equal(new DateOnly(2026, 10, 10), result.Value!.NextRunOn);
    }

    [Fact]
    public async Task Create_on_wallet_of_other_group_returns_not_found()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();

        var result = await CreateService(db).Service.CreateAsync(TestDb.OwnerId, NewRequest(TestDb.OtherGroupWalletId), Ct);

        Assert.Equal(RecurringResultStatus.NotFound, result.Status);
        Assert.Equal(0, await db.RecurringRules.CountAsync(Ct));
    }

    [Fact]
    public async Task Create_with_category_of_other_wallet_is_invalid()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var request = new CreateRecurringRuleRequest
        {
            WalletId = TestDb.WalletId,
            CategoryId = TestDb.OtherWalletCategoryId,
            Amount = 1m,
            Frequency = RecurrenceFrequency.Daily,
            StartDate = Today
        };

        var result = await CreateService(db).Service.CreateAsync(TestDb.OwnerId, request, Ct);

        Assert.Equal(RecurringResultStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("CategoryId"));
    }

    [Fact]
    public async Task Create_with_end_date_in_past_is_invalid()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var request = new CreateRecurringRuleRequest
        {
            WalletId = TestDb.WalletId,
            Amount = 1m,
            Frequency = RecurrenceFrequency.Daily,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 2, 1)
        };

        var result = await CreateService(db).Service.CreateAsync(TestDb.OwnerId, request, Ct);

        Assert.Equal(RecurringResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Outsider_cannot_read_or_list_rule()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using var db = testDb.CreateContext();
        var service = CreateService(db).Service;

        Assert.Equal(RecurringResultStatus.NotFound, (await service.GetAsync(TestDb.OutsiderId, rule.Id, Ct)).Status);
        Assert.Empty(await service.ListAsync(TestDb.OutsiderId, null, Ct));
        Assert.Single(await service.ListAsync(TestDb.OwnerId, null, Ct));
        Assert.Equal(RecurringResultStatus.NotFound,
            (await service.GetRunsAsync(TestDb.OutsiderId, rule.Id, new RecurringRunLogQuery(), Ct)).Status);
    }

    [Fact]
    public async Task Update_schedule_recalculates_next_run()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using var db = testDb.CreateContext();
        var request = new UpdateRecurringRuleRequest
        {
            CategoryId = TestDb.CategoryId,
            Amount = 3_000_000m,
            Frequency = RecurrenceFrequency.Weekly,
            DayOfWeek = 1,
            StartDate = rule.StartDate,
            RowVersion = rule.RowVersion
        };

        var result = await CreateService(db).Service.UpdateAsync(TestDb.OwnerId, rule.Id, request, Ct);

        Assert.Equal(RecurringResultStatus.Ok, result.Status);
        Assert.Equal(new DateOnly(2026, 10, 12), result.Value!.NextRunOn);
        Assert.Equal(3_000_000m, result.Value.Amount);
        Assert.Equal(rule.RowVersion + 1, result.Value.RowVersion);
    }

    [Fact]
    public async Task Reactivate_continues_from_today_without_backfill()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r =>
        {
            r.IsActive = false;
            r.NextRunOn = new DateOnly(2026, 9, 1);
        });
        await using var db = testDb.CreateContext();

        var result = await CreateService(db).Service.SetActiveAsync(
            TestDb.OwnerId, rule.Id, new SetRecurringRuleActiveRequest { IsActive = true, RowVersion = rule.RowVersion }, Ct);

        Assert.True(result.Value!.IsActive);
        Assert.Equal(Today, result.Value.NextRunOn);
    }

    [Fact]
    public async Task Delete_rule_with_run_history_is_conflict()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using (var setup = testDb.CreateContext())
        {
            setup.RecurringRunLogs.Add(new RecurringRunLog
            {
                RecurringRuleId = rule.Id,
                OccurrenceDate = Today,
                Status = (short)RecurringRunStatus.Skipped,
                RunAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await CreateService(db).Service.DeleteAsync(TestDb.OwnerId, rule.Id, Ct);

        Assert.Equal(RecurringResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Run_logs_are_filtered_by_status()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using (var setup = testDb.CreateContext())
        {
            setup.RecurringRunLogs.AddRange(
                new RecurringRunLog { RecurringRuleId = rule.Id, OccurrenceDate = Today.AddDays(-2), Status = (short)RecurringRunStatus.Created, RunAt = DateTime.UtcNow },
                new RecurringRunLog { RecurringRuleId = rule.Id, OccurrenceDate = Today.AddDays(-1), Status = (short)RecurringRunStatus.Failed, Error = "boom", RunAt = DateTime.UtcNow });
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await CreateService(db).Service.GetRunsAsync(
            TestDb.OwnerId, rule.Id, new RecurringRunLogQuery { Status = RecurringRunStatus.Failed }, Ct);

        var log = Assert.Single(result.Value!);
        Assert.Equal("Failed", log.Status);
        Assert.Equal("boom", log.Error);
    }

    [Fact]
    public async Task Trigger_enqueues_once()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using var db = testDb.CreateContext();
        var (service, queue) = CreateService(db);

        Assert.True((await service.TriggerAsync(TestDb.OwnerId, rule.Id, Ct)).Value!.Enqueued);
        Assert.False((await service.TriggerAsync(TestDb.OwnerId, rule.Id, Ct)).Value!.Enqueued);
        Assert.True(queue.IsPending(rule.Id));
        Assert.Equal(RecurringResultStatus.NotFound, (await service.TriggerAsync(TestDb.OutsiderId, rule.Id, Ct)).Status);
    }
}
