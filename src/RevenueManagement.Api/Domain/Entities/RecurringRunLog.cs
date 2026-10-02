namespace RevenueManagement.Api.Domain.Entities;

public sealed class RecurringRunLog
{
    public long Id { get; set; }

    public long RecurringRuleId { get; set; }

    public DateOnly OccurrenceDate { get; set; }

    public long? TransactionId { get; set; }

    public short Status { get; set; }

    public string? Error { get; set; }

    public DateTime RunAt { get; set; }

    // Navigation properties

    public RecurringRule RecurringRule { get; set; } = null!;

    public RevenueTransaction? Transaction { get; set; }
}
