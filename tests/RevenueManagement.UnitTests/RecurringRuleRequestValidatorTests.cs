using System.ComponentModel.DataAnnotations;
using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Recurring;
using Xunit;

namespace RevenueManagement.UnitTests;

/// <summary>
/// 1B.3 — Kiểm tra binding/validation của request quy tắc định kỳ đúng như [ApiController]
/// sẽ chạy (DataAnnotations + IValidatableObject).
/// </summary>
public sealed class RecurringRuleRequestValidatorTests
{
    private static CreateRecurringRuleRequest Valid(
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly,
        decimal? amount = 1_500_000m,
        short? dayOfWeek = null,
        short? dayOfMonth = 5,
        DateOnly? start = null,
        DateOnly? end = null,
        string? timeZoneId = null,
        int interval = 1)
        => new()
        {
            WalletId = 100,
            CategoryId = 1000,
            Amount = amount,
            Frequency = frequency,
            IntervalValue = interval,
            DayOfWeek = dayOfWeek,
            DayOfMonth = dayOfMonth,
            StartDate = start ?? new DateOnly(2026, 10, 7),
            EndDate = end,
            TimeZoneId = timeZoneId
        };

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static bool HasErrorFor(List<ValidationResult> results, string member)
        => results.Any(r => r.MemberNames.Contains(member));

    [Fact]
    public void Valid_monthly_request_passes()
        => Assert.Empty(Validate(Valid()));

    [Fact]
    public void Missing_required_fields_fail()
    {
        var results = Validate(new CreateRecurringRuleRequest());
        Assert.True(HasErrorFor(results, nameof(CreateRecurringRuleRequest.WalletId)));
        Assert.True(HasErrorFor(results, nameof(CreateRecurringRuleRequest.Amount)));
        Assert.True(HasErrorFor(results, nameof(CreateRecurringRuleRequest.Frequency)));
        Assert.True(HasErrorFor(results, nameof(CreateRecurringRuleRequest.StartDate)));
    }

    [Fact]
    public void Zero_amount_fails()
        => Assert.True(HasErrorFor(Validate(Valid(amount: 0m)), nameof(CreateRecurringRuleRequest.Amount)));

    [Fact]
    public void End_before_start_fails()
        => Assert.True(HasErrorFor(
            Validate(Valid(start: new DateOnly(2026, 10, 7), end: new DateOnly(2026, 10, 6))),
            nameof(CreateRecurringRuleRequest.EndDate)));

    [Fact]
    public void Day_of_week_only_allowed_for_weekly()
    {
        Assert.True(HasErrorFor(Validate(Valid(RecurrenceFrequency.Daily, dayOfWeek: 1, dayOfMonth: null)),
            nameof(CreateRecurringRuleRequest.DayOfWeek)));
        Assert.Empty(Validate(Valid(RecurrenceFrequency.Weekly, dayOfWeek: 1, dayOfMonth: null)));
    }

    [Fact]
    public void Day_of_month_only_allowed_for_monthly()
        => Assert.True(HasErrorFor(Validate(Valid(RecurrenceFrequency.Weekly, dayOfMonth: 5)),
            nameof(CreateRecurringRuleRequest.DayOfMonth)));

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)8)]
    public void Day_of_week_out_of_range_fails(short dayOfWeek)
        => Assert.True(HasErrorFor(Validate(Valid(RecurrenceFrequency.Weekly, dayOfWeek: dayOfWeek, dayOfMonth: null)),
            nameof(CreateRecurringRuleRequest.DayOfWeek)));

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)32)]
    public void Day_of_month_out_of_range_fails(short dayOfMonth)
        => Assert.True(HasErrorFor(Validate(Valid(dayOfMonth: dayOfMonth)),
            nameof(CreateRecurringRuleRequest.DayOfMonth)));

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void Interval_out_of_range_fails(int interval)
        => Assert.True(HasErrorFor(Validate(Valid(interval: interval)),
            nameof(CreateRecurringRuleRequest.IntervalValue)));

    [Fact]
    public void Unknown_time_zone_fails()
        => Assert.True(HasErrorFor(Validate(Valid(timeZoneId: "Mars/Olympus")),
            nameof(CreateRecurringRuleRequest.TimeZoneId)));

    [Fact]
    public void Undefined_frequency_value_fails()
    {
        var request = new CreateRecurringRuleRequest
        {
            WalletId = 100,
            Amount = 1m,
            Frequency = (RecurrenceFrequency)99,
            StartDate = new DateOnly(2026, 10, 7)
        };
        Assert.True(HasErrorFor(Validate(request), nameof(CreateRecurringRuleRequest.Frequency)));
    }

    [Fact]
    public void Error_dictionary_groups_messages_by_field()
    {
        var errors = RecurringRuleRequestValidator.ToErrorDictionary(
            RecurringRuleRequestValidator.Validate(Valid(amount: -5m, end: new DateOnly(2026, 1, 1))));
        Assert.True(errors.ContainsKey("Amount"));
        Assert.True(errors.ContainsKey("EndDate"));
    }
}
