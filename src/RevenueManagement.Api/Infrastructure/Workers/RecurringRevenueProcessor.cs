using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Recurring;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Infrastructure.Workers;

public enum OccurrenceOutcome
{
    Created,
    Skipped,
    /// <summary>Kỳ này đã có RunLog (lần chạy trước hoặc worker khác đã xử lý) → không tạo thêm.</summary>
    AlreadyProcessed,
    /// <summary>Quy tắc đã đổi giữa lúc đọc và lúc ghi (tắt, sửa lịch...) → đọc lại.</summary>
    Stale
}

public sealed record RecurringProcessResult(long RuleId, int Created, int Skipped, int AlreadyProcessed)
{
    public int Total => Created + Skipped + AlreadyProcessed;
}

/// <summary>
/// Xử lý các kỳ đến hạn của một quy tắc (scoped, một DbContext cho mỗi job).
///
/// Mỗi kỳ chạy trong MỘT database transaction:
///   kiểm tra RunLog (RuleId, OccurrenceDate) → tạo Transaction + RunLog Created
///   (hoặc RunLog Skipped) → cập nhật LastRunOn/NextRunOn → commit.
///
/// Chống tạo trùng ba lớp:
///   1. Hàng đợi không nhận trùng RuleId đang chờ.
///   2. Kiểm tra RunLog đã tồn tại trong transaction.
///   3. Unique index uq_recurring_rule_occurrence (RuleId, OccurrenceDate) và
///      unique IdempotencyKey "rr:{ruleId}:{yyyyMMdd}" trên transactions — nếu hai worker
///      chạy song song, bên commit sau nhận lỗi unique và bỏ qua kỳ đó.
/// RowVersion của quy tắc là concurrency token nên cập nhật NextRunOn cũng không bị ghi đè.
/// </summary>
public sealed class RecurringRevenueProcessor(
    AppDbContext db,
    TimeProvider clock,
    IOptions<WorkerOptions> options,
    IRevenueChangedPublisher publisher,
    ILogger<RecurringRevenueProcessor> logger)
{
    public const int ErrorMaxLength = 1000;

    public static string BuildIdempotencyKey(long ruleId, DateOnly occurrence)
        => $"rr:{ruleId}:{occurrence:yyyyMMdd}";

    /// <summary>
    /// Xử lý tất cả kỳ đã đến hạn (NextRunOn &lt;= hôm nay theo múi giờ của quy tắc),
    /// tối đa MaxCatchUpOccurrences kỳ. Lỗi hạ tầng (mất kết nối DB...) được ném ra để worker retry.
    /// </summary>
    public async Task<RecurringProcessResult> ProcessRuleAsync(long ruleId, CancellationToken cancellationToken)
    {
        int created = 0, skipped = 0, alreadyProcessed = 0;
        var changedGroups = new HashSet<long>();
        var maxOccurrences = options.Value.MaxCatchUpOccurrences;

        for (var guard = 0; guard < maxOccurrences * 2; guard++)
        {
            if (created + skipped + alreadyProcessed >= maxOccurrences) break;

            db.ChangeTracker.Clear();
            var due = await db.RecurringRules
                .AsNoTracking()
                .Where(r => r.Id == ruleId && r.IsActive && r.NextRunOn != null)
                .Select(r => new { r.NextRunOn, r.TimeZoneId })
                .FirstOrDefaultAsync(cancellationToken);

            if (due is null) break;

            var today = RecurringSchedule.GetLocalDate(due.TimeZoneId, clock.GetUtcNow());
            var occurrence = due.NextRunOn!.Value;
            if (occurrence > today) break;

            OccurrenceOutcome outcome;
            long? groupId;
            try
            {
                (outcome, groupId) = await ProcessOccurrenceAsync(ruleId, occurrence, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                logger.LogInformation(
                    "Recurring rule {RuleId} occurrence {OccurrenceDate} was already processed by another worker",
                    ruleId, occurrence);
                outcome = OccurrenceOutcome.AlreadyProcessed;
                groupId = null;
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogInformation(
                    "Recurring rule {RuleId} changed while processing {OccurrenceDate}; reloading",
                    ruleId, occurrence);
                continue;
            }

            switch (outcome)
            {
                case OccurrenceOutcome.Created:
                    created++;
                    if (groupId is { } g) changedGroups.Add(g);
                    break;
                case OccurrenceOutcome.Skipped:
                    skipped++;
                    break;
                case OccurrenceOutcome.AlreadyProcessed:
                    alreadyProcessed++;
                    break;
                case OccurrenceOutcome.Stale:
                    continue;
            }
        }

        // Chỉ phát sự kiện sau khi các transaction đã commit.
        foreach (var groupId in changedGroups)
        {
            try
            {
                await publisher.PublishAsync(groupId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Dữ liệu đã lưu; lỗi thông báo không được làm job thất bại/retry (tránh tạo trùng).
                logger.LogWarning(ex, "Publishing RevenueChanged for group {GroupId} failed", groupId);
            }
        }

        return new RecurringProcessResult(ruleId, created, skipped, alreadyProcessed);
    }

    /// <summary>
    /// Gọi khi worker đã retry hết: ghi RunLog Failed cho kỳ đang đến hạn và dời NextRunOn
    /// sang kỳ sau để lỗi một kỳ không làm kẹt toàn bộ lịch.
    /// Trả về ngày kỳ bị đánh dấu Failed (null nếu không có kỳ nào đến hạn).
    /// </summary>
    public async Task<DateOnly?> RecordFailureAsync(long ruleId, Exception error, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var rule = await db.RecurringRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
            if (rule?.NextRunOn is not { } occurrence) return (DateOnly?)null;

            var today = RecurringSchedule.GetLocalDate(rule.TimeZoneId, clock.GetUtcNow());
            if (occurrence > today) return null;

            var exists = await db.RecurringRunLogs
                .AnyAsync(l => l.RecurringRuleId == ruleId && l.OccurrenceDate == occurrence, ct);

            if (!exists)
            {
                db.RecurringRunLogs.Add(new RecurringRunLog
                {
                    RecurringRuleId = ruleId,
                    OccurrenceDate = occurrence,
                    Status = (short)RecurringRunStatus.Failed,
                    Error = FormatError(error),
                    RunAt = clock.GetUtcNow().UtcDateTime
                });
            }

            Advance(rule, occurrence);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return occurrence;
        }, cancellationToken);
    }

    private async Task<(OccurrenceOutcome Outcome, long? GroupId)> ProcessOccurrenceAsync(
        long ruleId,
        DateOnly occurrence,
        CancellationToken cancellationToken)
    {
        // EnableRetryOnFailure của Npgsql bắt buộc transaction do user mở phải chạy trong execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var rule = await db.RecurringRules
                .Include(r => r.Wallet).ThenInclude(w => w.Group)
                .Include(r => r.Category)
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.Id == ruleId, ct);

            if (rule is null || !rule.IsActive || rule.NextRunOn != occurrence)
                return (OccurrenceOutcome.Stale, (long?)null);

            var groupId = rule.Wallet.GroupId;
            var alreadyLogged = await db.RecurringRunLogs
                .AnyAsync(l => l.RecurringRuleId == ruleId && l.OccurrenceDate == occurrence, ct);

            var now = clock.GetUtcNow().UtcDateTime;
            OccurrenceOutcome outcome;

            if (alreadyLogged)
            {
                outcome = OccurrenceOutcome.AlreadyProcessed;
            }
            else if (await GetSkipReasonAsync(rule, ct) is { } reason)
            {
                db.RecurringRunLogs.Add(new RecurringRunLog
                {
                    RecurringRuleId = ruleId,
                    OccurrenceDate = occurrence,
                    Status = (short)RecurringRunStatus.Skipped,
                    Error = reason,
                    RunAt = now
                });
                outcome = OccurrenceOutcome.Skipped;
            }
            else
            {
                // TODO(A/C): khi RevenueService của A có hàm tạo dùng chung DbContext/transaction,
                // chuyển sang gọi hàm đó thay vì khởi tạo entity tại đây.
                var revenue = new RevenueTransaction
                {
                    WalletId = rule.WalletId,
                    CategoryId = rule.CategoryId,
                    CreatedByUserId = rule.CreatedByUserId,
                    Amount = rule.Amount,
                    Kind = TransactionKind.Income,
                    OccurredOn = occurrence,
                    Note = rule.Note,
                    RecurringRuleId = rule.Id,
                    IdempotencyKey = BuildIdempotencyKey(rule.Id, occurrence),
                    CreatedAt = now,
                    RowVersion = 1
                };

                db.RevenueTransactions.Add(revenue);
                db.RecurringRunLogs.Add(new RecurringRunLog
                {
                    RecurringRuleId = ruleId,
                    OccurrenceDate = occurrence,
                    Transaction = revenue,
                    Status = (short)RecurringRunStatus.Created,
                    RunAt = now
                });
                outcome = OccurrenceOutcome.Created;
            }

            Advance(rule, occurrence);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "Recurring rule {RuleId} occurrence {OccurrenceDate}: {Outcome}; next run {NextRunOn}",
                ruleId, occurrence, outcome, rule.NextRunOn);

            return (outcome, (long?)groupId);
        }, cancellationToken);
    }

    private async Task<string?> GetSkipReasonAsync(RecurringRule rule, CancellationToken ct)
    {
        if (rule.Kind != TransactionKind.Income) return "Quy tắc không phải loại Income.";
        if (rule.Wallet.Group.IsDeleted) return "Nhóm đã bị xóa.";
        if (rule.Wallet.IsArchived) return "Ví đã được lưu trữ.";
        if (rule.Category is { } category)
        {
            if (category.IsArchived) return "Danh mục đã được lưu trữ.";
            if (category.Kind != TransactionKind.Income) return "Danh mục không phải loại Income.";
            if (category.WalletId != rule.WalletId) return "Danh mục không thuộc ví của quy tắc.";
        }
        if (!rule.CreatedByUser.IsActive) return "Tài khoản người tạo quy tắc đã bị khóa.";

        var isMember = await db.GroupMembers.AnyAsync(
            m => m.GroupId == rule.Wallet.GroupId && m.UserId == rule.CreatedByUserId && m.LeftAt == null, ct);
        return isMember ? null : "Người tạo quy tắc không còn là thành viên của nhóm.";
    }

    private static void Advance(RecurringRule rule, DateOnly occurrence)
    {
        rule.LastRunOn = occurrence;
        rule.NextRunOn = RecurringSchedule.CalculateNextRunOn(RecurrenceSpec.From(rule), occurrence);
        rule.RowVersion++;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Chỉ lưu loại lỗi + message (không stack trace, không connection string), cắt 1000 ký tự.</summary>
    public static string FormatError(Exception error)
    {
        var root = error;
        while (root.InnerException is not null) root = root.InnerException;
        var message = root == error
            ? $"{error.GetType().Name}: {error.Message}"
            : $"{error.GetType().Name}: {error.Message} -> {root.GetType().Name}: {root.Message}";
        return message.Length <= ErrorMaxLength ? message : message[..ErrorMaxLength];
    }
}
