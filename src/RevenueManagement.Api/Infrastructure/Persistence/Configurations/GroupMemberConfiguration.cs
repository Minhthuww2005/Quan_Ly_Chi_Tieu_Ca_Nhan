using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class GroupMemberConfiguration
    : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");

        builder.HasKey(x => x.Id)
            .HasName("group_members_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasColumnType("smallint")
            .HasDefaultValue((short)2);

        builder.Property(x => x.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.LeftAt)
            .HasColumnName("left_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("idx_group_members_group");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("idx_group_members_user");

        builder.HasIndex(x => new { x.GroupId, x.UserId })
            .IsUnique()
            .HasDatabaseName("uq_group_members_group_user");

        builder.HasOne(x => x.Group)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_group_members_group");

        builder.HasOne(x => x.User)
            .WithMany(x => x.GroupMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_group_members_user");
    }
}
