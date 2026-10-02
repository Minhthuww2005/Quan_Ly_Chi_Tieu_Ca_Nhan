using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence.Configurations;

public sealed class RecurringRunLogConfiguration
    : IEntityTypeConfiguration<RecurringRunLog>
{
    public void Configure(EntityTypeBuilder<RecurringRunLog> builder)
    {
        builder.ToTable("recurring_run_logs");

        builder.HasKey(x => x.Id)
            .HasName("recurring_run_logs_pkey");

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.RecurringRuleId)
            .HasColumnName("recurring_rule_id");

        builder.Property(x => x.OccurrenceDate)
            .HasColumnName("occurrence_date")
            .HasColumnType("date");

        builder.Property(x => x.TransactionId)
            .HasColumnName("transaction_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("smallint");

        builder.Property(x => x.Error)
            .HasColumnName("error")
            .HasMaxLength(1000);

        builder.Property(x => x.RunAt)
            .HasColumnName("run_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.RecurringRuleId)
            .HasDatabaseName("idx_recurring_run_logs_rule");

        builder.HasIndex(
                x => new
                {
                    x.RecurringRuleId,
                    x.OccurrenceDate
                })
            .IsUnique()
            .HasDatabaseName("uq_recurring_rule_occurrence");

        builder.HasIndex(x => x.TransactionId)
            .IsUnique()
            .HasDatabaseName("recurring_run_logs_transaction_id_key");

        builder.HasOne(x => x.RecurringRule)
            .WithMany(x => x.RunLogs)
            .HasForeignKey(x => x.RecurringRuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_recurring_run_logs_rule");

        builder.HasOne(x => x.Transaction)
            .WithOne(x => x.RecurringRunLog)
            .HasForeignKey<RecurringRunLog>(x => x.TransactionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_recurring_run_logs_transaction");
    }
}
