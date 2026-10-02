using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class RevenueTransactionConfiguration
    : IEntityTypeConfiguration<RevenueTransaction>
{
    public void Configure(
        EntityTypeBuilder<RevenueTransaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(x => x.Id)
            .HasName("transactions_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.WalletId)
            .HasColumnName("wallet_id");

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id");

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(x => x.OccurredOn)
            .HasColumnName("occurred_on")
            .HasColumnType("date")
            .HasDefaultValueSql("CURRENT_DATE");

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(x => x.RecurringRuleId)
            .HasColumnName("recurring_rule_id");

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("idx_transactions_category");

        builder.HasIndex(x => x.CreatedByUserId)
            .HasDatabaseName("idx_transactions_created_by");

        builder.HasIndex(x => x.OccurredOn)
            .HasDatabaseName("idx_transactions_occurred_on");

        builder.HasIndex(x => x.RecurringRuleId)
            .HasDatabaseName("idx_transactions_recurring_rule");

        builder.HasIndex(x => x.WalletId)
            .HasDatabaseName("idx_transactions_wallet");

        builder.HasIndex(x => new { x.WalletId, x.OccurredOn })
            .HasDatabaseName("idx_transactions_wallet_date");

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("transactions_idempotency_key_key");

        builder.HasOne(x => x.Wallet)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transactions_wallet");

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_transactions_category");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedTransactions)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transactions_user");

        builder.HasOne(x => x.RecurringRule)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.RecurringRuleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_transactions_recurring_rule");
    }
}
