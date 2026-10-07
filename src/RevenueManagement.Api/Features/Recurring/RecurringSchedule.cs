using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// Thông số lịch lặp, tách khỏi entity để tính toán thuần (pure) và unit test độc lập.
/// DayOfWeek theo ISO 8601, khớp CHECK constraint day_of_week 1..7: 1 = Thứ Hai ... 7 = Chủ nhật.
/// </summary>
public sealed record RecurrenceSpec(
    RecurrenceFrequency Frequency,
    int IntervalValue,
    short? DayOfWeek,
    short? DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    public static RecurrenceSpec From(RecurringRule rule) => new(
        rule.Frequency,
        rule.IntervalValue,
        rule.DayOfWeek,
        rule.DayOfMonth,
        rule.StartDate,
        rule.EndDate);
}

/// <summary>
/// Tính lịch chạy của quy tắc định kỳ. Daily/Weekly/Monthly chỉ là tham số Frequency,
/// không tách thành ba module riêng.
///
/// Quy tắc:
/// - Daily: StartDate, StartDate + n*Interval ngày.
/// - Weekly: lần đầu là ngày DayOfWeek đầu tiên &gt;= StartDate (mặc định thứ của StartDate),
///   sau đó cộng 7*Interval ngày.
/// - Monthly: ngày neo = DayOfMonth (mặc định ngày của StartDate). Tháng thiếu ngày neo
///   (29/30/31) thì chạy vào ngày cuối tháng; tháng sau quay lại ngày neo, không bị trôi.
/// - Không có lần chạy nào sau EndDate (trả về null).
/// </summary>
public static class RecurringSchedule
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>Lần chạy đầu tiên của quy tắc (&gt;= StartDate), null nếu đã quá EndDate.</summary>
    public static DateOnly? CalculateFirstRunOn(RecurrenceSpec spec)
        => CalculateRunOnOrAfter(spec, spec.StartDate);

    /// <summary>Lần chạy kế tiếp, đứng sau <paramref name="lastRunOn"/>.</summary>
    public static DateOnly? CalculateNextRunOn(RecurrenceSpec spec, DateOnly lastRunOn)
        => lastRunOn == DateOnly.MaxValue ? null : CalculateRunOnOrAfter(spec, lastRunOn.AddDays(1));

    /// <summary>Lần chạy đầu tiên rơi vào hoặc sau ngày <paramref name="date"/>.</summary>
    public static DateOnly? CalculateRunOnOrAfter(RecurrenceSpec spec, DateOnly date)
    {
        EnsureValid(spec);

        var first = FirstOccurrence(spec);
        DateOnly candidate;

        if (date <= first)
        {
            candidate = first;
        }
        else
        {
            switch (spec.Frequency)
            {
                case RecurrenceFrequency.Daily:
                case RecurrenceFrequency.Weekly:
                {
                    var step = spec.Frequency == RecurrenceFrequency.Daily
                        ? spec.IntervalValue
                        : 7 * spec.IntervalValue;
                    var diff = date.DayNumber - first.DayNumber;
                    var periods = (diff + step - 1) / step;
                    var dayNumber = (long)first.DayNumber + (long)periods * step;
                    if (dayNumber > DateOnly.MaxValue.DayNumber) return null;
                    candidate = DateOnly.FromDayNumber((int)dayNumber);
                    break;
                }
                case RecurrenceFrequency.Monthly:
                {
                    var anchor = AnchorDay(spec);
                    var monthsDiff = (date.Year - first.Year) * 12 + date.Month - first.Month;
                    var periods = Math.Max(0, monthsDiff / spec.IntervalValue);
                    while (true)
                    {
                        candidate = MonthlyOccurrence(first, periods * spec.IntervalValue, anchor);
                        if (candidate >= date) break;
                        periods++;
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Frequency, "Frequency không hợp lệ.");
            }
        }

        return spec.EndDate is { } end && candidate > end ? null : candidate;
    }

    /// <summary>Ngày hiện tại theo múi giờ của quy tắc; múi giờ không hợp lệ thì dùng UTC.</summary>
    public static DateOnly GetLocalDate(string? timeZoneId, DateTimeOffset utcNow)
    {
        var local = TryFindTimeZone(timeZoneId, out var timeZone)
            ? TimeZoneInfo.ConvertTime(utcNow, timeZone)
            : utcNow.ToUniversalTime();
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static bool TryFindTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId)) return false;

        try
        {
            // .NET 6+ hỗ trợ cả IANA id (Asia/Ho_Chi_Minh) lẫn Windows id trên mọi hệ điều hành.
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static DateOnly FirstOccurrence(RecurrenceSpec spec)
    {
        var start = spec.StartDate;
        switch (spec.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return start;
            case RecurrenceFrequency.Weekly:
            {
                var startDay = IsoDayOfWeek(start);
                var target = spec.DayOfWeek ?? startDay;
                var offset = (target - startDay + 7) % 7;
                return start.AddDays(offset);
            }
            case RecurrenceFrequency.Monthly:
            {
                var anchor = AnchorDay(spec);
                var inStartMonth = MonthlyOccurrence(start, 0, anchor);
                return inStartMonth >= start ? inStartMonth : MonthlyOccurrence(start, 1, anchor);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(spec), spec.Frequency, "Frequency không hợp lệ.");
        }
    }

    /// <summary>1 = Thứ Hai ... 7 = Chủ nhật (ISO 8601).</summary>
    public static int IsoDayOfWeek(DateOnly date)
        => date.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static int AnchorDay(RecurrenceSpec spec) => spec.DayOfMonth ?? spec.StartDate.Day;

    private static DateOnly MonthlyOccurrence(DateOnly reference, int monthsToAdd, int anchorDay)
    {
        var month = new DateOnly(reference.Year, reference.Month, 1).AddMonths(monthsToAdd);
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(month.Year, month.Month));
        return new DateOnly(month.Year, month.Month, day);
    }

    private static void EnsureValid(RecurrenceSpec spec)
    {
        if (!Enum.IsDefined(spec.Frequency))
            throw new ArgumentOutOfRangeException(nameof(spec), spec.Frequency, "Frequency không hợp lệ.");
        if (spec.IntervalValue < 1)
            throw new ArgumentOutOfRangeException(nameof(spec), spec.IntervalValue, "IntervalValue phải >= 1.");
        if (spec.DayOfWeek is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(spec), spec.DayOfWeek, "DayOfWeek phải trong 1..7.");
        if (spec.DayOfMonth is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(spec), spec.DayOfMonth, "DayOfMonth phải trong 1..31.");
    }
}
