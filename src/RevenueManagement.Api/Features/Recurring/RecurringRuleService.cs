using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Revenues;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Infrastructure.Workers;

namespace RevenueManagement.Api.Features.Recurring;

public enum RecurringResultStatus
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Kết quả use case; controller map sang 200/201/400/404/409.</summary>
public sealed record RecurringResult<T>(
    RecurringResultStatus Status,
    T? Value = default,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? Message = null)
{
    public static RecurringResult<T> Ok(T value) => new(RecurringResultStatus.Ok, value);
    public static RecurringResult<T> NotFound() => new(RecurringResultStatus.NotFound);
    public static RecurringResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(RecurringResultStatus.Invalid, Errors: errors);
    public static RecurringResult<T> Invalid(string field, string message) => Invalid(new Dictionary<string, string[]> { [field] = [message] });
    public static RecurringResult<T> Conflict(string message) => new(RecurringResultStatus.Conflict, Message: message);
}

/// <summary>
/// REV-03 — CRUD quy tắc doanh thu định kỳ và xem nhật ký chạy.
/// Mọi truy vấn lọc theo membership của user (không tin GroupId/UserId từ client) để chống IDOR;
/// quy tắc của nhóm khác trả về 404.
/// </summary>
public sealed class RecurringRuleService(
    AppDbContext db,
    RevenueValidator revenueValidator,
    RevenueJobQueue queue,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<RecurringRuleResponse>> ListAsync(
        long userId,
        long? walletId,
        CancellationToken cancellationToken)
    {
        var query = AccessibleRules(userId).AsNoTracking();
        if (walletId is { } id) query = query.Where(r => r.WalletId == id);

        return await query
            .OrderBy(r => r.NextRunOn == null)
            .ThenBy(r => r.NextRunOn)
            .ThenBy(r => r.Id)
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<RecurringResult<RecurringRuleResponse>> GetAsync(
        long userId,
        long ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await AccessibleRules(userId)
            .AsNoTracking()
            .Where(r => r.Id == ruleId)
            .Select(ToResponseExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return rule is null
            ? RecurringResult<RecurringRuleResponse>.NotFound()
            : RecurringResult<RecurringRuleResponse>.Ok(rule);
    }

    public async Task<RecurringResult<RecurringRuleResponse>> CreateAsync(
        long userId,
        CreateRecurringRuleRequest request,
        CancellationToken cancellationToken)
    {
        var shapeErrors = ValidateShape(request);
        if (shapeErrors.Count > 0) return RecurringResult<RecurringRuleResponse>.Invalid(shapeErrors);

        var references = await revenueValidator.ValidateReferencesAsync(
            userId, request.WalletId!.Value, request.CategoryId, cancellationToken);
        if (references.WalletNotFound) return RecurringResult<RecurringRuleResponse>.NotFound();
        if (!references.IsValid) return RecurringResult<RecurringRuleResponse>.Invalid(references.Errors);

        var rule = new RecurringRule
        {
            WalletId = request.WalletId.Value,
            CreatedByUserId = userId,
            Kind = TransactionKind.Income,
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            RowVersion = 1
        };
        Apply(rule, request);

        rule.NextRunOn = CalculateUpcomingRun(rule);
        if (rule.NextRunOn is null)
            return RecurringResult<RecurringRuleResponse>.Invalid(nameof(request.EndDate),
                "Khoảng StartDate–EndDate không còn lần chạy nào kể từ hôm nay.");

        db.RecurringRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);

        return RecurringResult<RecurringRuleResponse>.Ok(ToResponse(rule, references.GroupId!.Value));
    }

    public async Task<RecurringResult<RecurringRuleResponse>> UpdateAsync(
        long userId,
        long ruleId,
        UpdateRecurringRuleRequest request,
        CancellationToken cancellationToken)
    {
        var shapeErrors = ValidateShape(request);
        if (shapeErrors.Count > 0) return RecurringResult<RecurringRuleResponse>.Invalid(shapeErrors);

        var rule = await AccessibleRules(userId).FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
        if (rule is null) return RecurringResult<RecurringRuleResponse>.NotFound();

        var references = await revenueValidator.ValidateReferencesAsync(
            userId, rule.WalletId, request.CategoryId, cancellationToken);
        if (references.WalletNotFound) return RecurringResult<RecurringRuleResponse>.NotFound();
        if (!references.IsValid) return RecurringResult<RecurringRuleResponse>.Invalid(references.Errors);

        var oldSpec = RecurrenceSpec.From(rule);
        Apply(rule, request);

        if (RecurrenceSpec.From(rule) != oldSpec && rule.IsActive)
        {
            rule.NextRunOn = CalculateUpcomingRun(rule);
            if (rule.NextRunOn is null)
                return RecurringResult<RecurringRuleResponse>.Invalid(nameof(request.EndDate),
                    "Khoảng StartDate–EndDate không còn lần chạy nào kể từ hôm nay.");
        }

        return await SaveWithConcurrencyAsync(rule, request.RowVersion, references.GroupId!.Value, cancellationToken);
    }

    public async Task<RecurringResult<RecurringRuleResponse>> SetActiveAsync(
        long userId,
        long ruleId,
        SetRecurringRuleActiveRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await AccessibleRules(userId)
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
        if (rule is null) return RecurringResult<RecurringRuleResponse>.NotFound();

        var activate = request.IsActive!.Value;
        if (activate && !rule.IsActive)
        {
            // Bật lại: không bù các kỳ trong thời gian tắt, chạy tiếp từ hôm nay.
            rule.NextRunOn = CalculateUpcomingRun(rule);
            if (rule.NextRunOn is null)
                return RecurringResult<RecurringRuleResponse>.Invalid(nameof(rule.EndDate),
                    "Quy tắc đã quá EndDate, không thể bật lại.");
        }

        rule.IsActive = activate;
        return await SaveWithConcurrencyAsync(rule, request.RowVersion, rule.Wallet.GroupId, cancellationToken);
    }

    public async Task<RecurringResult<bool>> DeleteAsync(long userId, long ruleId, CancellationToken cancellationToken)
    {
        var rule = await AccessibleRules(userId).FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
        if (rule is null) return RecurringResult<bool>.NotFound();

        if (await db.RecurringRunLogs.AnyAsync(l => l.RecurringRuleId == ruleId, cancellationToken))
            return RecurringResult<bool>.Conflict(
                "Quy tắc đã có lịch sử chạy; hãy tắt quy tắc (IsActive = false) để giữ nhật ký.");

        db.RecurringRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return RecurringResult<bool>.Ok(true);
    }

    public async Task<RecurringResult<IReadOnlyList<RecurringRunLogResponse>>> GetRunsAsync(
        long userId,
        long ruleId,
        RecurringRunLogQuery query,
        CancellationToken cancellationToken)
    {
        if (!await AccessibleRules(userId).AnyAsync(r => r.Id == ruleId, cancellationToken))
            return RecurringResult<IReadOnlyList<RecurringRunLogResponse>>.NotFound();

        var logs = db.RecurringRunLogs.AsNoTracking().Where(l => l.RecurringRuleId == ruleId);
        if (query.Status is { } status) logs = logs.Where(l => l.Status == (short)status);
        if (query.From is { } from) logs = logs.Where(l => l.OccurrenceDate >= from);
        if (query.To is { } to) logs = logs.Where(l => l.OccurrenceDate <= to);

        var rows = await logs
            .OrderByDescending(l => l.OccurrenceDate)
            .ThenByDescending(l => l.Id)
            .Take(query.Limit)
            .Select(l => new { l.Id, l.RecurringRuleId, l.OccurrenceDate, l.Status, l.TransactionId, l.Error, l.RunAt })
            .ToListAsync(cancellationToken);

        IReadOnlyList<RecurringRunLogResponse> result = rows
            .Select(l => new RecurringRunLogResponse(
                l.Id, l.RecurringRuleId, l.OccurrenceDate, StatusName(l.Status), l.TransactionId, l.Error, l.RunAt))
            .ToList();
        return RecurringResult<IReadOnlyList<RecurringRunLogResponse>>.Ok(result);
    }

    /// <summary>Đưa quy tắc vào hàng đợi ngay (phục vụ demo/kiểm thử), không chờ chu kỳ quét.</summary>
    public async Task<RecurringResult<RecurringTriggerResponse>> TriggerAsync(
        long userId,
        long ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await AccessibleRules(userId)
            .AsNoTracking()
            .Where(r => r.Id == ruleId)
            .Select(r => new { r.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (rule is null) return RecurringResult<RecurringTriggerResponse>.NotFound();
        if (!rule.IsActive) return RecurringResult<RecurringTriggerResponse>.Conflict("Quy tắc đang tắt.");

        var enqueued = await queue.EnqueueAsync(ruleId, cancellationToken);
        return RecurringResult<RecurringTriggerResponse>.Ok(new RecurringTriggerResponse(
            ruleId,
            enqueued,
            enqueued ? "Đã đưa quy tắc vào hàng đợi." : "Quy tắc đang có trong hàng đợi hoặc đang được xử lý."));
    }

    public static string StatusName(short status)
        => Enum.IsDefined(typeof(RecurringRunStatus), status)
            ? ((RecurringRunStatus)status).ToString()
            : $"Unknown({status})";

    private IQueryable<RecurringRule> AccessibleRules(long userId)
        => db.RecurringRules.Where(r =>
            !r.Wallet.Group.IsDeleted &&
            r.Wallet.Group.Members.Any(m => m.UserId == userId && m.LeftAt == null));

    private static Dictionary<string, string[]> ValidateShape(RecurringRuleRequestBase request)
        => RecurringRuleRequestValidator.ToErrorDictionary(RecurringRuleRequestValidator.Validate(request));

    private static void Apply(RecurringRule rule, RecurringRuleRequestBase request)
    {
        rule.CategoryId = request.CategoryId;
        rule.Amount = request.Amount!.Value;
        rule.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        rule.Frequency = request.Frequency!.Value;
        rule.IntervalValue = request.IntervalValue;
        rule.DayOfWeek = request.DayOfWeek;
        rule.DayOfMonth = request.DayOfMonth;
        rule.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? RecurringSchedule.DefaultTimeZoneId
            : request.TimeZoneId.Trim();
        rule.StartDate = request.StartDate!.Value;
        rule.EndDate = request.EndDate;
    }

    /// <summary>
    /// Lần chạy sắp tới tính từ hôm nay (theo múi giờ quy tắc), sau LastRunOn.
    /// Không chạy bù quá khứ khi tạo mới, sửa lịch hoặc bật lại.
    /// </summary>
    private DateOnly? CalculateUpcomingRun(RecurringRule rule)
    {
        var today = RecurringSchedule.GetLocalDate(rule.TimeZoneId, clock.GetUtcNow());
        var from = today;
        if (rule.LastRunOn is { } last && last >= from) from = last.AddDays(1);
        return RecurringSchedule.CalculateRunOnOrAfter(RecurrenceSpec.From(rule), from);
    }

    private async Task<RecurringResult<RecurringRuleResponse>> SaveWithConcurrencyAsync(
        RecurringRule rule,
        long expectedRowVersion,
        long groupId,
        CancellationToken cancellationToken)
    {
        // RowVersion là concurrency token: UPDATE ... WHERE row_version = <giá trị client gửi>.
        db.Entry(rule).Property(r => r.RowVersion).OriginalValue = expectedRowVersion;
        rule.RowVersion = expectedRowVersion + 1;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RecurringResult<RecurringRuleResponse>.Conflict(
                "Quy tắc đã bị thay đổi (bởi người dùng khác hoặc worker). Hãy tải lại rồi thử lại.");
        }

        return RecurringResult<RecurringRuleResponse>.Ok(ToResponse(rule, groupId));
    }

    private static RecurringRuleResponse ToResponse(RecurringRule r, long groupId) => new(
        r.Id, groupId, r.WalletId, r.CategoryId, r.CreatedByUserId, r.Amount, r.Note, r.Frequency,
        r.IntervalValue, r.DayOfWeek, r.DayOfMonth, r.TimeZoneId, r.StartDate, r.EndDate,
        r.NextRunOn, r.LastRunOn, r.IsActive, r.CreatedAt, r.RowVersion);

    private static readonly System.Linq.Expressions.Expression<Func<RecurringRule, RecurringRuleResponse>> ToResponseExpression =
        r => new RecurringRuleResponse(
            r.Id, r.Wallet.GroupId, r.WalletId, r.CategoryId, r.CreatedByUserId, r.Amount, r.Note, r.Frequency,
            r.IntervalValue, r.DayOfWeek, r.DayOfMonth, r.TimeZoneId, r.StartDate, r.EndDate,
            r.NextRunOn, r.LastRunOn, r.IsActive, r.CreatedAt, r.RowVersion);
}
