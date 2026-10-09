using System.Globalization;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Recurring;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RecurringScheduleTests
{
    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static RecurrenceSpec Spec(
        RecurrenceFrequency frequency,
        string start,
        int interval = 1,
        short? dayOfWeek = null,
        short? dayOfMonth = null,
        string? end = null)
        => new(frequency, interval, dayOfWeek, dayOfMonth, D(start), end is null ? null : D(end));

    // ---------- Daily ----------

    [Theory]
    [InlineData("2026-10-07", 1, "2026-10-07", "2026-10-08")]
    [InlineData("2026-10-07", 3, "2026-10-07", "2026-10-10")]
    [InlineData("2026-12-31", 1, "2026-12-31", "2027-01-01")]
    [InlineData("2028-02-28", 1, "2028-02-28", "2028-02-29")]
    public void Daily_next_run_adds_interval_days(string start, int interval, string last, string expected)
    {
        var next = RecurringSchedule.CalculateNextRunOn(Spec(RecurrenceFrequency.Daily, start, interval), D(last));
        Assert.Equal(D(expected), next);
    }

    [Fact]
    public void Daily_first_run_is_start_date()
        => Assert.Equal(D("2026-10-07"), RecurringSchedule.CalculateFirstRunOn(Spec(RecurrenceFrequency.Daily, "2026-10-07")));

    [Fact]
    public void Daily_on_or_after_stays_on_interval_grid()
    {
        // Start 01/10, mỗi 3 ngày: 01, 04, 07, 10 ... → từ 08/10 thì lần kế là 10/10.
        var spec = Spec(RecurrenceFrequency.Daily, "2026-10-01", interval: 3);
        Assert.Equal(D("2026-10-10"), RecurringSchedule.CalculateRunOnOrAfter(spec, D("2026-10-08")));
        Assert.Equal(D("2026-10-07"), RecurringSchedule.CalculateRunOnOrAfter(spec, D("2026-10-07")));
    }

    // ---------- Weekly ----------

    [Fact]
    public void Weekly_first_run_moves_to_requested_day_of_week()
    {
        // 07/10/2026 là thứ Tư; DayOfWeek = 1 (thứ Hai) → 12/10/2026.
        var spec = Spec(RecurrenceFrequency.Weekly, "2026-10-07", dayOfWeek: 1);
        Assert.Equal(D("2026-10-12"), RecurringSchedule.CalculateFirstRunOn(spec));
    }

    [Fact]
    public void Weekly_without_day_of_week_uses_start_day()
    {
        var spec = Spec(RecurrenceFrequency.Weekly, "2026-10-07");
        Assert.Equal(D("2026-10-07"), RecurringSchedule.CalculateFirstRunOn(spec));
        Assert.Equal(D("2026-10-14"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-10-07")));
    }

    [Fact]
    public void Weekly_sunday_is_seven_in_iso_numbering()
    {
        // 07/10/2026 là thứ Tư; DayOfWeek = 7 (Chủ nhật) → 11/10/2026.
        var spec = Spec(RecurrenceFrequency.Weekly, "2026-10-07", dayOfWeek: 7);
        Assert.Equal(D("2026-10-11"), RecurringSchedule.CalculateFirstRunOn(spec));
        Assert.Equal(7, RecurringSchedule.IsoDayOfWeek(D("2026-10-11")));
        Assert.Equal(1, RecurringSchedule.IsoDayOfWeek(D("2026-10-12")));
    }

    [Fact]
    public void Enum_values_match_database_check_constraints()
    {
        Assert.Equal(1, (int)RecurrenceFrequency.Daily);
        Assert.Equal(2, (int)RecurrenceFrequency.Weekly);
        Assert.Equal(3, (int)RecurrenceFrequency.Monthly);
        Assert.Equal(1, (int)TransactionKind.Income);
    }

    [Fact]
    public void Weekly_crosses_year_boundary()
    {
        // 28/12/2026 là thứ Hai.
        var spec = Spec(RecurrenceFrequency.Weekly, "2026-12-28", dayOfWeek: 1);
        Assert.Equal(D("2027-01-04"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-12-28")));
    }

    [Fact]
    public void Weekly_with_interval_two_skips_a_week()
    {
        var spec = Spec(RecurrenceFrequency.Weekly, "2026-10-05", interval: 2, dayOfWeek: 1);
        Assert.Equal(D("2026-10-19"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-10-05")));
        Assert.Equal(D("2026-10-19"), RecurringSchedule.CalculateRunOnOrAfter(spec, D("2026-10-06")));
    }

    // ---------- Monthly ----------

    [Fact]
    public void Monthly_day_31_falls_back_to_last_day_and_returns_to_31()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2026-01-31", dayOfMonth: 31);
        var feb = RecurringSchedule.CalculateNextRunOn(spec, D("2026-01-31"));
        Assert.Equal(D("2026-02-28"), feb);

        var mar = RecurringSchedule.CalculateNextRunOn(spec, feb!.Value);
        Assert.Equal(D("2026-03-31"), mar);

        var apr = RecurringSchedule.CalculateNextRunOn(spec, mar!.Value);
        Assert.Equal(D("2026-04-30"), apr);
    }

    [Fact]
    public void Monthly_day_29_uses_leap_day_in_leap_year()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2028-01-29", dayOfMonth: 29);
        Assert.Equal(D("2028-02-29"), RecurringSchedule.CalculateNextRunOn(spec, D("2028-01-29")));
    }

    [Fact]
    public void Monthly_first_run_moves_to_next_month_when_anchor_already_passed()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2026-10-20", dayOfMonth: 5);
        Assert.Equal(D("2026-11-05"), RecurringSchedule.CalculateFirstRunOn(spec));
    }

    [Fact]
    public void Monthly_without_day_of_month_uses_start_day()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2026-10-15");
        Assert.Equal(D("2026-11-15"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-10-15")));
    }

    [Fact]
    public void Monthly_interval_three_is_quarterly_across_year()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2026-11-10", interval: 3, dayOfMonth: 10);
        Assert.Equal(D("2027-02-10"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-11-10")));
        Assert.Equal(D("2027-02-10"), RecurringSchedule.CalculateRunOnOrAfter(spec, D("2026-12-01")));
    }

    // ---------- EndDate / validation ----------

    [Fact]
    public void Returns_null_after_end_date()
    {
        var spec = Spec(RecurrenceFrequency.Daily, "2026-10-01", end: "2026-10-07");
        Assert.Equal(D("2026-10-07"), RecurringSchedule.CalculateNextRunOn(spec, D("2026-10-06")));
        Assert.Null(RecurringSchedule.CalculateNextRunOn(spec, D("2026-10-07")));
    }

    [Fact]
    public void Returns_null_when_first_occurrence_is_after_end_date()
    {
        var spec = Spec(RecurrenceFrequency.Monthly, "2026-10-20", dayOfMonth: 5, end: "2026-10-31");
        Assert.Null(RecurringSchedule.CalculateFirstRunOn(spec));
    }

    [Fact]
    public void Invalid_interval_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringSchedule.CalculateFirstRunOn(Spec(RecurrenceFrequency.Daily, "2026-10-01", interval: 0)));

    // ---------- Time zone ----------

    [Fact]
    public void Local_date_uses_rule_time_zone()
    {
        // 18:00 UTC ngày 06/10 = 01:00 ngày 07/10 ở Việt Nam (UTC+7).
        var utcNow = new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero);
        Assert.Equal(D("2026-10-07"), RecurringSchedule.GetLocalDate("Asia/Ho_Chi_Minh", utcNow));
        Assert.Equal(D("2026-10-06"), RecurringSchedule.GetLocalDate("UTC", utcNow));
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_utc()
    {
        var utcNow = new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero);
        Assert.False(RecurringSchedule.TryFindTimeZone("Mars/Olympus", out _));
        Assert.Equal(D("2026-10-06"), RecurringSchedule.GetLocalDate("Mars/Olympus", utcNow));
    }
}
