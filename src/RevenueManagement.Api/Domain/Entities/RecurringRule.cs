using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Domain.Entities;

public sealed class RecurringRule
{
    public long Id { get; set; }

    public long WalletId { get; set; }

    public long? CategoryId { get; set; }

    public long CreatedByUserId { get; set; }

    public decimal Amount { get; set; }

    public TransactionKind Kind { get; set; }

    public string? Note { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    public int IntervalValue { get; set; }

    public short? DayOfMonth { get; set; }

    public short? DayOfWeek { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateOnly? NextRunOn { get; set; }

    public DateOnly? LastRunOn { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public Wallet Wallet { get; set; } = null!;

    public Category? Category { get; set; }

    public User CreatedByUser { get; set; } = null!;

    public ICollection<RevenueTransaction> Transactions { get; set; }
        = new List<RevenueTransaction>();

    public ICollection<RecurringRunLog> RunLogs { get; set; }
        = new List<RecurringRunLog>();
}
