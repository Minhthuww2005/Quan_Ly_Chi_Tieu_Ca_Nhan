namespace RevenueManagement.Api.Domain.Entities;

public sealed class BudgetAlert
{
    public long Id { get; set; }

    public long BudgetId { get; set; }

    public short TriggerLevel { get; set; }

    public decimal SpentAmount { get; set; }

    public decimal LimitAmount { get; set; }

    public short Status { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime TriggeredAt { get; set; }

    public DateTime? SentAt { get; set; }

    // Navigation properties

    public Budget Budget { get; set; } = null!;
}
