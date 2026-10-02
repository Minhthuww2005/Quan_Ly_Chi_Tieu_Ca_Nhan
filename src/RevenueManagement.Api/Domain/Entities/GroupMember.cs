namespace RevenueManagement.Api.Domain.Entities;

public sealed class GroupMember
{
    public long Id { get; set; }

    public long GroupId { get; set; }

    public long UserId { get; set; }

    public short Role { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    // Navigation properties

    public Group Group { get; set; } = null!;

    public User User { get; set; } = null!;
}