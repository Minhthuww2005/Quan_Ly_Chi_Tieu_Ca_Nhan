namespace RevenueManagement.Api.Domain.Entities;

public sealed class Budget
{
    public long Id { get; set; }

    public long WalletId { get; set; }

    public long? CategoryId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public decimal LimitAmount { get; set; }

    public short WarnThresholdPercent { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public Wallet Wallet { get; set; } = null!;

    public Category? Category { get; set; }

    public ICollection<BudgetAlert> Alerts { get; set; }
        = new List<BudgetAlert>();
}
