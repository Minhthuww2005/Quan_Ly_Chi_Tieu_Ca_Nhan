using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration
    : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(x => x.Id)
            .HasName("budgets_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.WalletId)
            .HasColumnName("wallet_id");

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id");

        builder.Property(x => x.PeriodStart)
            .HasColumnName("period_start")
            .HasColumnType("date");

        builder.Property(x => x.LimitAmount)
            .HasColumnName("limit_amount")
            .HasPrecision(19, 4);

        builder.Property(x => x.WarnThresholdPercent)
            .HasColumnName("warn_threshold_percent")
            .HasColumnType("smallint")
            .HasDefaultValue((short)80);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("idx_budgets_category");

        builder.HasIndex(x => x.WalletId)
            .HasDatabaseName("idx_budgets_wallet");

        builder.HasOne(x => x.Wallet)
            .WithMany(x => x.Budgets)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_budgets_wallet");

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Budgets)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_budgets_category");
    }
}
