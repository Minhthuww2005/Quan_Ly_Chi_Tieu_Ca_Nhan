using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Domain.Entities;

public sealed class Category
{
    public long Id { get; set; }

    public long WalletId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TransactionKind Kind { get; set; }

    public string? Icon { get; set; }

    public bool IsArchived { get; set; }

    // Navigation properties

    public Wallet Wallet { get; set; } = null!;

    public ICollection<RevenueTransaction> Transactions { get; set; }
        = new List<RevenueTransaction>();

    public ICollection<RecurringRule> RecurringRules { get; set; }
        = new List<RecurringRule>();

    public ICollection<Budget> Budgets { get; set; }
        = new List<Budget>();
}
