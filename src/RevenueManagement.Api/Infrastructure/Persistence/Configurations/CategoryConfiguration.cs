using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration
    : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(x => x.Id)
            .HasName("categories_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.WalletId)
            .HasColumnName("wallet_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<short>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(x => x.Icon)
            .HasColumnName("icon")
            .HasMaxLength(50);

        builder.Property(x => x.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.HasIndex(x => x.WalletId)
            .HasDatabaseName("idx_categories_wallet");

        builder.HasIndex(x => new
            {
                x.WalletId,
                x.Name,
                x.Kind
            })
            .IsUnique()
            .HasDatabaseName("uq_categories_wallet_name_kind");

        builder.HasOne(x => x.Wallet)
            .WithMany(x => x.Categories)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_categories_wallet");
    }
}
