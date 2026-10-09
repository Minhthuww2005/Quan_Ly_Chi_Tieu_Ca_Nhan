using System.ComponentModel.DataAnnotations;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Revenues;

namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// Ràng buộc chéo giữa các trường của request quy tắc định kỳ.
/// Hàm thuần, không truy cập database, để unit test trực tiếp.
/// Kiểm tra Wallet/Category/quyền thành viên nằm ở RevenueValidator.ValidateReferencesAsync.
/// </summary>
public static class RecurringRuleRequestValidator
{
    public static readonly DateOnly MinStartDate = new(2000, 1, 1);
    public static readonly DateOnly MaxStartDate = new(2100, 12, 31);

    public static IEnumerable<ValidationResult> Validate(RecurringRuleRequestBase request)
    {
        if (request.Amount is { } amount)
        {
            foreach (var error in RevenueValidator.ValidateAmount(amount))
                yield return new ValidationResult(error, [nameof(request.Amount)]);
        }

        if (request.StartDate is { } start)
        {
            if (start < MinStartDate || start > MaxStartDate)
            {
                yield return new ValidationResult(
                    $"StartDate phải nằm trong khoảng {MinStartDate:yyyy-MM-dd} đến {MaxStartDate:yyyy-MM-dd}.",
                    [nameof(request.StartDate)]);
            }

            if (request.EndDate is { } end && end < start)
            {
                yield return new ValidationResult(
                    "EndDate phải lớn hơn hoặc bằng StartDate.",
                    [nameof(request.EndDate)]);
            }
        }

        if (request.DayOfWeek is not null && request.Frequency != RecurrenceFrequency.Weekly)
        {
            yield return new ValidationResult(
                "DayOfWeek chỉ dùng khi Frequency = Weekly.",
                [nameof(request.DayOfWeek)]);
        }

        if (request.DayOfMonth is not null && request.Frequency != RecurrenceFrequency.Monthly)
        {
            yield return new ValidationResult(
                "DayOfMonth chỉ dùng khi Frequency = Monthly.",
                [nameof(request.DayOfMonth)]);
        }

        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)
            && !RecurringSchedule.TryFindTimeZone(request.TimeZoneId, out _))
        {
            yield return new ValidationResult(
                $"TimeZoneId '{request.TimeZoneId}' không tồn tại.",
                [nameof(request.TimeZoneId)]);
        }
    }

    /// <summary>Dùng trong service khi cần dict lỗi (400) thay vì ModelState.</summary>
    public static Dictionary<string, string[]> ToErrorDictionary(IEnumerable<ValidationResult> results)
        => results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { string.Empty })
                .Select(member => (member, message: r.ErrorMessage ?? "Dữ liệu không hợp lệ.")))
            .GroupBy(x => x.member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.message).ToArray());
}
