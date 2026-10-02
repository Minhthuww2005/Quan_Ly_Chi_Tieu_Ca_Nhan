namespace RevenueManagement.Api.Domain.Entities;

public sealed class RefreshToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public long? ReplacedById { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties

    public User User { get; set; } = null!;

    public RefreshToken? ReplacedBy { get; set; }

    public ICollection<RefreshToken> ReplacedTokens { get; set; }
        = new List<RefreshToken>();
}
