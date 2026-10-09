using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Domain.Entities;

public sealed class RevenueTransaction
{
    public long Id { get; set; }

    public long WalletId { get; set; }

    public long? CategoryId { get; set; }

    public long CreatedByUserId { get; set; }

    public decimal Amount { get; set; }

    public TransactionKind Kind { get; set; }

    public DateOnly OccurredOn { get; set; }

    public string? Note { get; set; }

    public long? RecurringRuleId { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public Wallet Wallet { get; set; } = null!;

    public Category? Category { get; set; }

    public User CreatedByUser { get; set; } = null!;

    public RecurringRule? RecurringRule { get; set; }

    public RecurringRunLog? RecurringRunLog { get; set; }
}