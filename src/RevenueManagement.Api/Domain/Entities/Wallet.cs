namespace RevenueManagement.Api.Domain.Entities;

public sealed class Wallet
{
    public long Id { get; set; }

    public long GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal InitialBalance { get; set; }

    public int StatsVersion { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public Group Group { get; set; } = null!;

    public ICollection<Category> Categories { get; set; }
        = new List<Category>();

    public ICollection<RevenueTransaction> Transactions { get; set; }
        = new List<RevenueTransaction>();

    public ICollection<RecurringRule> RecurringRules { get; set; }
        = new List<RecurringRule>();

    public ICollection<Budget> Budgets { get; set; }
        = new List<Budget>();
}