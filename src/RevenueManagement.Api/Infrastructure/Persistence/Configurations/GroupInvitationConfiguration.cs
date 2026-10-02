using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class GroupInvitationConfiguration
    : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> builder)
    {
        builder.ToTable("group_invitations");

        builder.HasKey(x => x.Id)
            .HasName("group_invitations_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id");

        builder.Property(x => x.InvitedEmail)
            .HasColumnName("invited_email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasColumnType("smallint")
            .HasDefaultValue((short)2);

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.InvitedByUserId)
            .HasColumnName("invited_by_user_id");

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.AcceptedAt)
            .HasColumnName("accepted_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.InvitedEmail)
            .HasDatabaseName("idx_group_invitations_email");

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("idx_group_invitations_group");

        builder.HasOne(x => x.Group)
            .WithMany(x => x.Invitations)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_group_invitations_group");

        builder.HasOne(x => x.InvitedByUser)
            .WithMany(x => x.SentGroupInvitations)
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_group_invitations_user");
    }
}
