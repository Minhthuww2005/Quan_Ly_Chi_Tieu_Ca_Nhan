using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Enums;

namespace RevenueManagement.Api.Features.Reports;

/// <summary>
/// Read-only EF Core context for REV-02. It maps the same tables owned by the core
/// migration without creating a separate report table or duplicating write logic.
/// </summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<ReportTransaction> Transactions => Set<ReportTransaction>();
    public DbSet<ReportWallet> Wallets => Set<ReportWallet>();
    public DbSet<ReportCategory> Categories => Set<ReportCategory>();
    public DbSet<ReportGroupMember> GroupMembers => Set<ReportGroupMember>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("ReportingDbContext chỉ dùng để đọc.");

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("ReportingDbContext chỉ dùng để đọc.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportTransaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.WalletId).HasColumnName("wallet_id");
            entity.Property(item => item.CategoryId).HasColumnName("category_id");
            entity.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(19, 4);
            entity.Property(item => item.Kind).HasColumnName("kind").HasConversion<short>();
            entity.Property(item => item.OccurredOn).HasColumnName("occurred_on");
            entity.Property(item => item.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(item => new { item.WalletId, item.OccurredOn });
            entity.HasIndex(item => new { item.CategoryId, item.OccurredOn });
        });

        modelBuilder.Entity<ReportWallet>(entity =>
        {
            entity.ToTable("wallets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(100);
        });

        modelBuilder.Entity<ReportCategory>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.WalletId).HasColumnName("wallet_id");
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(100);
        });

        modelBuilder.Entity<ReportGroupMember>(entity =>
        {
            entity.ToTable("group_members");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.LeftAt).HasColumnName("left_at");
            entity.HasIndex(item => new { item.GroupId, item.UserId });
        });
    }
}

public sealed class ReportTransaction
{
    public long Id { get; init; }
    public long WalletId { get; init; }
    public long? CategoryId { get; init; }
    public long CreatedByUserId { get; init; }
    public decimal Amount { get; init; }
    public DateOnly OccurredOn { get; init; }
    public TransactionKind Kind { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class ReportWallet
{
    public long Id { get; init; }
    public long GroupId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ReportCategory
{
    public long Id { get; init; }
    public long WalletId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ReportGroupMember
{
    public long Id { get; init; }
    public long GroupId { get; init; }
    public long UserId { get; init; }
    public DateTime? LeftAt { get; init; }
}
