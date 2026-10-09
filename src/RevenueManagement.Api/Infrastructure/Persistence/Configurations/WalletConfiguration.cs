using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets");

        builder.HasKey(x => x.Id)
            .HasName("wallets_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.InitialBalance)
            .HasColumnName("initial_balance")
            .HasPrecision(19, 4)
            .HasDefaultValue(0m);

        builder.Property(x => x.StatsVersion)
            .HasColumnName("stats_version")
            .HasDefaultValue(0);

        builder.Property(x => x.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("idx_wallets_group");

        builder.HasIndex(x => new { x.GroupId, x.Name })
            .IsUnique()
            .HasDatabaseName("uq_wallets_group_name");

        builder.HasOne(x => x.Group)
            .WithMany(x => x.Wallets)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wallets_group");
    }
}
