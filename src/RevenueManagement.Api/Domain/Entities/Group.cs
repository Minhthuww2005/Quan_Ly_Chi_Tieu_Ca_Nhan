namespace RevenueManagement.Api.Domain.Entities;

public sealed class Group
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public short Type { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public long RowVersion { get; set; }

    // Navigation properties

    public User CreatedByUser { get; set; } = null!;

    public ICollection<GroupMember> Members { get; set; }
        = new List<GroupMember>();

    public ICollection<GroupInvitation> Invitations { get; set; }
        = new List<GroupInvitation>();

    public ICollection<Wallet> Wallets { get; set; }
        = new List<Wallet>();
}
