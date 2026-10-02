namespace RevenueManagement.Api.Domain.Entities;

public sealed class User
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public ICollection<Group> CreatedGroups { get; set; }
        = new List<Group>();

    public ICollection<GroupMember> GroupMemberships { get; set; }
        = new List<GroupMember>();

    public ICollection<GroupInvitation> SentGroupInvitations { get; set; }
        = new List<GroupInvitation>();

    public ICollection<RevenueTransaction> CreatedTransactions { get; set; }
        = new List<RevenueTransaction>();

    public ICollection<RecurringRule> CreatedRecurringRules { get; set; }
        = new List<RecurringRule>();

    public ICollection<RefreshToken> RefreshTokens { get; set; }
        = new List<RefreshToken>();
}
