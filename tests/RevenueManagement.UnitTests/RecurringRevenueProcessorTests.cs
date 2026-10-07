using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RevenueManagement.Api.Features.Recurring;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.Api.Options;
using RevenueManagement.UnitTests.TestSupport;
using Xunit;

namespace RevenueManagement.UnitTests;

/// <summary>
/// 1B.4 / 3.2 — Worker tạo giao dịch định kỳ: idempotency, chạy bù, Skipped/Failed, múi giờ.
/// "Hôm nay" mặc định là 07/10/2026 theo giờ Việt Nam (03:00 UTC = 10:00 ICT).
/// </summary>
public sealed class RecurringRevenueProcessorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 7);

    private sealed class RecordingPublisher : IRevenueChangedPublisher
    {
        public List<long> Groups { get; } = [];

        public Task PublishAsync(long groupId, CancellationToken cancellationToken = default)
        {
            Groups.Add(groupId);
            return Task.CompletedTask;
        }
    }

    private static RecurringRevenueProcessor CreateProcessor(
        AppDbContext db,
        TestTimeProvider? clock = null,
        WorkerOptions? options = null,
        IRevenueChangedPublisher? publisher = null)
        => new(
            db,
            clock ?? new TestTimeProvider(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero)),
            Microsoft.Extensions.Options.Options.Create(options ?? new WorkerOptions()),
            publisher ?? new RecordingPublisher(),
            NullLogger<RecurringRevenueProcessor>.Instance);

    [Fact]
    public async Task Due_occurrence_creates_one_transaction_and_created_log()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        var publisher = new RecordingPublisher();

        await using (var db = testDb.CreateContext())
        {
            var result = await CreateProcessor(db, publisher: publisher).ProcessRuleAsync(rule.Id, Ct);
            Assert.Equal(1, result.Created);
        }

        await using var check = testDb.CreateContext();
        var transaction = await check.RevenueTransactions.SingleAsync(Ct);
        Assert.Equal(rule.Amount, transaction.Amount);
        Assert.Equal(Today, transaction.OccurredOn);
        Assert.Equal(rule.Id, transaction.RecurringRuleId);
        Assert.Equal(TestDb.OwnerId, transaction.CreatedByUserId);
        Assert.Equal(RecurringRevenueProcessor.BuildIdempotencyKey(rule.Id, Today), transaction.IdempotencyKey);

        var log = await check.RecurringRunLogs.SingleAsync(Ct);
        Assert.Equal((short)RecurringRunStatus.Created, log.Status);
        Assert.Equal(transaction.Id, log.TransactionId);

        var updated = await check.RecurringRules.SingleAsync(Ct);
        Assert.Equal(Today, updated.LastRunOn);
        Assert.Equal(Today.AddDays(1), updated.NextRunOn);
        Assert.Equal(2, updated.RowVersion);

        Assert.Equal(new[] { TestDb.GroupId }, publisher.Groups);
    }

    [Fact]
    public async Task Running_same_occurrence_twice_creates_only_one_transaction()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();

        await using (var db = testDb.CreateContext())
            await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        await using (var db = testDb.CreateContext())
        {
            var second = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);
            Assert.Equal(0, second.Total);
        }

        await using var check = testDb.CreateContext();
        Assert.Equal(1, await check.RevenueTransactions.CountAsync(Ct));
        Assert.Equal(1, await check.RecurringRunLogs.CountAsync(Ct));
    }

    [Fact]
    public async Task Stale_next_run_from_another_worker_does_not_duplicate()
    {
        // Mô phỏng worker thứ hai đọc quy tắc trước khi worker đầu commit:
        // NextRunOn vẫn trỏ về kỳ đã xử lý nhưng RunLog của kỳ đó đã tồn tại.
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();

        await using (var db = testDb.CreateContext())
            await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        await using (var db = testDb.CreateContext())
        {
            var stale = await db.RecurringRules.SingleAsync(Ct);
            stale.NextRunOn = Today;
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = testDb.CreateContext())
        {
            var result = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);
            Assert.Equal(0, result.Created);
            Assert.Equal(1, result.AlreadyProcessed);
        }

        await using var check = testDb.CreateContext();
        Assert.Equal(1, await check.RevenueTransactions.CountAsync(Ct));
        Assert.Equal(Today.AddDays(1), (await check.RecurringRules.SingleAsync(Ct)).NextRunOn);
    }

    [Fact]
    public async Task Missed_days_are_caught_up_once_each()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r =>
        {
            r.StartDate = new DateOnly(2026, 10, 1);
            r.NextRunOn = new DateOnly(2026, 10, 4);
        });

        await using (var db = testDb.CreateContext())
        {
            var result = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);
            Assert.Equal(4, result.Created);
        }

        await using var check = testDb.CreateContext();
        var dates = await check.RevenueTransactions.OrderBy(t => t.OccurredOn).Select(t => t.OccurredOn).ToListAsync(Ct);
        Assert.Equal(
            new[] { new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6), Today },
            dates);
    }

    [Fact]
    public async Task Catch_up_is_limited_per_run()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r =>
        {
            r.StartDate = new DateOnly(2026, 9, 1);
            r.NextRunOn = new DateOnly(2026, 9, 1);
        });

        await using var db = testDb.CreateContext();
        var result = await CreateProcessor(db, options: new WorkerOptions { MaxCatchUpOccurrences = 3 })
            .ProcessRuleAsync(rule.Id, Ct);

        Assert.Equal(3, result.Created);
        Assert.Equal(new DateOnly(2026, 9, 4), (await db.RecurringRules.AsNoTracking().SingleAsync(Ct)).NextRunOn);
    }

    [Fact]
    public async Task Future_occurrence_is_not_processed()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r => r.NextRunOn = Today.AddDays(1));

        await using var db = testDb.CreateContext();
        var result = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, await db.RevenueTransactions.CountAsync(Ct));
    }

    [Fact]
    public async Task Inactive_rule_is_not_processed()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r => r.IsActive = false);

        await using var db = testDb.CreateContext();
        Assert.Equal(0, (await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct)).Total);
    }

    [Fact]
    public async Task Rule_ends_after_end_date()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule(r => r.EndDate = Today);

        await using var db = testDb.CreateContext();
        await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        var updated = await db.RecurringRules.AsNoTracking().SingleAsync(Ct);
        Assert.Null(updated.NextRunOn);
        Assert.Equal(Today, updated.LastRunOn);
    }

    [Fact]
    public async Task Archived_wallet_produces_skipped_log_without_transaction()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using (var setup = testDb.CreateContext())
        {
            (await setup.Wallets.SingleAsync(w => w.Id == TestDb.WalletId, Ct)).IsArchived = true;
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, await db.RevenueTransactions.CountAsync(Ct));
        var log = await db.RecurringRunLogs.SingleAsync(Ct);
        Assert.Equal((short)RecurringRunStatus.Skipped, log.Status);
        Assert.NotNull(log.Error);
        Assert.Equal(Today.AddDays(1), (await db.RecurringRules.AsNoTracking().SingleAsync(Ct)).NextRunOn);
    }

    [Fact]
    public async Task Creator_who_left_group_produces_skipped_log()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();
        await using (var setup = testDb.CreateContext())
        {
            (await setup.GroupMembers.SingleAsync(m => m.UserId == TestDb.OwnerId, Ct)).LeftAt = DateTime.UtcNow;
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await CreateProcessor(db).ProcessRuleAsync(rule.Id, Ct);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, await db.RevenueTransactions.CountAsync(Ct));
    }

    [Fact]
    public async Task Record_failure_writes_failed_log_and_advances_schedule()
    {
        var testDb = TestDb.CreateSeeded();
        var rule = testDb.AddRule();

        await using var db = testDb.CreateContext();
        var occurrence = await CreateProcessor(db)
            .RecordFailureAsync(rule.Id, new InvalidOperationException("database timeout"), Ct);

        Assert.Equal(Today, occurrence);
        var log = await db.RecurringRunLogs.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((short)RecurringRunStatus.Failed, log.Status);
        Assert.Contains("database timeout", log.Error);
        Assert.Null(log.TransactionId);
        Assert.Equal(Today.AddDays(1), (await db.RecurringRules.AsNoTracking().SingleAsync(Ct)).NextRunOn);
    }

    [Fact]
    public void Error_text_is_truncated_to_column_length()
    {
        var error = RecurringRevenueProcessor.FormatError(new Exception(new string('x', 5000)));
        Assert.Equal(RecurringRevenueProcessor.ErrorMaxLength, error.Length);
    }

    [Fact]
    public async Task Due_date_follows_rule_time_zone()
    {
        // 18:00 UTC 06/10 = 01:00 ICT 07/10: quy tắc giờ Việt Nam đã đến hạn, quy tắc UTC thì chưa.
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero));
        var testDb = TestDb.CreateSeeded();
        var vietnam = testDb.AddRule();
        var utc = testDb.AddRule(r => r.TimeZoneId = "UTC");

        await using var db = testDb.CreateContext();
        var processor = CreateProcessor(db, clock);

        Assert.Equal(1, (await processor.ProcessRuleAsync(vietnam.Id, Ct)).Created);
        Assert.Equal(0, (await processor.ProcessRuleAsync(utc.Id, Ct)).Created);
    }

    [Fact]
    public void Idempotency_key_fits_column()
    {
        var key = RecurringRevenueProcessor.BuildIdempotencyKey(long.MaxValue, new DateOnly(2026, 10, 7));
        Assert.Equal("rr:9223372036854775807:20261007", key);
        Assert.True(key.Length <= 64);
    }
}
