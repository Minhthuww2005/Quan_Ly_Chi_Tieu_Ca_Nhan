using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class BudgetAlertConfiguration
    : IEntityTypeConfiguration<BudgetAlert>
{
    public void Configure(EntityTypeBuilder<BudgetAlert> builder)
    {
        builder.ToTable("budget_alerts");

        builder.HasKey(x => x.Id)
            .HasName("budget_alerts_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BudgetId)
            .HasColumnName("budget_id");

        builder.Property(x => x.TriggerLevel)
            .HasColumnName("trigger_level")
            .HasColumnType("smallint");

        builder.Property(x => x.SpentAmount)
            .HasColumnName("spent_amount")
            .HasPrecision(19, 4);

        builder.Property(x => x.LimitAmount)
            .HasColumnName("limit_amount")
            .HasPrecision(19, 4);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("smallint")
            .HasDefaultValue((short)0);

        builder.Property(x => x.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0);

        builder.Property(x => x.NextAttemptAt)
            .HasColumnName("next_attempt_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.Property(x => x.TriggeredAt)
            .HasColumnName("triggered_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.BudgetId)
            .HasDatabaseName("idx_budget_alerts_budget");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_budget_alerts_status");

        builder.HasOne(x => x.Budget)
            .WithMany(x => x.Alerts)
            .HasForeignKey(x => x.BudgetId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_budget_alerts_budget");
    }
}
