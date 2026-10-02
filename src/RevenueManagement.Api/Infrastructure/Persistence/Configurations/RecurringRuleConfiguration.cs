using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class RecurringRuleConfiguration
    : IEntityTypeConfiguration<RecurringRule>
{
    public void Configure(EntityTypeBuilder<RecurringRule> builder)
    {
        builder.ToTable("recurring_rules");

        builder.HasKey(x => x.Id)
            .HasName("recurring_rules_pkey");

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
            .HasPrecision(19, 4);

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(x => x.IntervalValue)
            .HasColumnName("interval_value")
            .HasDefaultValue(1);

        builder.Property(x => x.DayOfMonth)
            .HasColumnName("day_of_month")
            .HasColumnType("smallint");

        builder.Property(x => x.DayOfWeek)
            .HasColumnName("day_of_week")
            .HasColumnType("smallint");

        builder.Property(x => x.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(64)
            .HasDefaultValue("Asia/Ho_Chi_Minh");

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

        builder.Property(x => x.NextRunOn)
            .HasColumnName("next_run_on")
            .HasColumnType("date");

        builder.Property(x => x.LastRunOn)
            .HasColumnName("last_run_on")
            .HasColumnType("date");

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
            .HasDatabaseName("idx_recurring_rules_category");

        builder.HasIndex(x => x.CreatedByUserId)
            .HasDatabaseName("idx_recurring_rules_created_by");

        builder.HasIndex(x => x.NextRunOn)
            .HasDatabaseName("idx_recurring_rules_next_run");

        builder.HasIndex(x => x.WalletId)
            .HasDatabaseName("idx_recurring_rules_wallet");

        builder.HasOne(x => x.Wallet)
            .WithMany(x => x.RecurringRules)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_recurring_rules_wallet");

        builder.HasOne(x => x.Category)
            .WithMany(x => x.RecurringRules)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_recurring_rules_category");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedRecurringRules)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recurring_rules_user");
    }
}
