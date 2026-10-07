using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// Trường dùng chung giữa tạo và cập nhật quy tắc.
/// Attribute kiểm tra từng trường; IValidatableObject kiểm tra ràng buộc chéo
/// (EndDate &gt;= StartDate, DayOfWeek chỉ cho Weekly...). Lỗi trả về 400 ValidationProblem.
/// </summary>
public abstract class RecurringRuleRequestBase : IValidatableObject
{
    [Range(1, long.MaxValue, ErrorMessage = "CategoryId không hợp lệ.")]
    public long? CategoryId { get; init; }

    [Required(ErrorMessage = "Amount là bắt buộc.")]
    public decimal? Amount { get; init; }

    [StringLength(500, ErrorMessage = "Note tối đa 500 ký tự.")]
    public string? Note { get; init; }

    [Required(ErrorMessage = "Frequency là bắt buộc (Daily, Weekly hoặc Monthly).")]
    [EnumDataType(typeof(RecurrenceFrequency), ErrorMessage = "Frequency phải là Daily, Weekly hoặc Monthly.")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecurrenceFrequency? Frequency { get; init; }

    [Range(1, 365, ErrorMessage = "IntervalValue phải từ 1 đến 365.")]
    public int IntervalValue { get; init; } = 1;

    /// <summary>ISO 8601: 1 = Thứ Hai ... 7 = Chủ nhật. Chỉ dùng với Weekly.</summary>
    [Range(1, 7, ErrorMessage = "DayOfWeek phải từ 1 (Thứ Hai) đến 7 (Chủ nhật).")]
    public short? DayOfWeek { get; init; }

    /// <summary>1..31. Chỉ dùng với Monthly; tháng thiếu ngày sẽ chạy vào ngày cuối tháng.</summary>
    [Range(1, 31, ErrorMessage = "DayOfMonth phải từ 1 đến 31.")]
    public short? DayOfMonth { get; init; }

    [StringLength(64, ErrorMessage = "TimeZoneId tối đa 64 ký tự.")]
    public string? TimeZoneId { get; init; }

    [Required(ErrorMessage = "StartDate là bắt buộc.")]
    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => RecurringRuleRequestValidator.Validate(this);
}

public sealed class CreateRecurringRuleRequest : RecurringRuleRequestBase
{
    [Required(ErrorMessage = "WalletId là bắt buộc.")]
    [Range(1, long.MaxValue, ErrorMessage = "WalletId không hợp lệ.")]
    public long? WalletId { get; init; }
}

/// <summary>Không cho đổi Wallet của quy tắc; muốn đổi ví thì tạo quy tắc mới.</summary>
public sealed class UpdateRecurringRuleRequest : RecurringRuleRequestBase
{
    /// <summary>RowVersion lấy từ lần đọc trước, dùng cho optimistic concurrency (409 nếu lệch).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "RowVersion là bắt buộc.")]
    public long RowVersion { get; init; }
}

public sealed class SetRecurringRuleActiveRequest
{
    [Required(ErrorMessage = "IsActive là bắt buộc.")]
    public bool? IsActive { get; init; }

    [Range(1, long.MaxValue, ErrorMessage = "RowVersion là bắt buộc.")]
    public long RowVersion { get; init; }
}

public sealed class RecurringRunLogQuery
{
    [EnumDataType(typeof(RecurringRunStatus), ErrorMessage = "Status phải là Created, Skipped hoặc Failed.")]
    public RecurringRunStatus? Status { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    [Range(1, 200, ErrorMessage = "Limit phải từ 1 đến 200.")]
    public int Limit { get; init; } = 50;
}

public sealed record RecurringRuleResponse(
    long Id,
    long GroupId,
    long WalletId,
    long? CategoryId,
    long CreatedByUserId,
    decimal Amount,
    string? Note,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RecurrenceFrequency Frequency,
    int IntervalValue,
    short? DayOfWeek,
    short? DayOfMonth,
    string TimeZoneId,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? NextRunOn,
    DateOnly? LastRunOn,
    bool IsActive,
    DateTime CreatedAt,
    long RowVersion);

public sealed record RecurringRunLogResponse(
    long Id,
    long RecurringRuleId,
    DateOnly OccurrenceDate,
    string Status,
    long? TransactionId,
    string? Error,
    DateTime RunAt);

public sealed record RecurringTriggerResponse(long RuleId, bool Enqueued, string Message);
