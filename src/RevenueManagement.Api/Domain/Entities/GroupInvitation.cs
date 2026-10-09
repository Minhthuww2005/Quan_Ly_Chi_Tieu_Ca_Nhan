namespace RevenueManagement.Api.Domain.Entities;

public sealed class GroupInvitation
{
    public long Id { get; set; }

    public long GroupId { get; set; }

    public string InvitedEmail { get; set; } = string.Empty;

    public short Role { get; set; }

    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public long InvitedByUserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties

    public Group Group { get; set; } = null!;

    public User InvitedByUser { get; set; } = null!;
}
