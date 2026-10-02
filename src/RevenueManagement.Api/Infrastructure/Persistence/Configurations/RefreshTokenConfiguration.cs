using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration
    : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id)
            .HasName("refresh_tokens_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ReplacedById)
            .HasColumnName("replaced_by_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("refresh_tokens_token_hash_key");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("idx_refresh_tokens_user");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_refresh_tokens_expires");

        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_tokens_user");

        builder.HasOne(x => x.ReplacedBy)
            .WithMany(x => x.ReplacedTokens)
            .HasForeignKey(x => x.ReplacedById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_refresh_tokens_replaced_by");
    }
}
