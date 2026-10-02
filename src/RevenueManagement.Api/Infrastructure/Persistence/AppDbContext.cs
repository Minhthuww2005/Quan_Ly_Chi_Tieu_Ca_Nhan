using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Domain.Entities;

namespace RevenueManagement.Api.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupInvitation> GroupInvitations
        => Set<GroupInvitation>();

    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<RevenueTransaction> RevenueTransactions
        => Set<RevenueTransaction>();

    public DbSet<RecurringRule> RecurringRules
        => Set<RecurringRule>();

    public DbSet<RecurringRunLog> RecurringRunLogs
        => Set<RecurringRunLog>();

    public DbSet<RefreshToken> RefreshTokens
        => Set<RefreshToken>();

    public DbSet<Budget> Budgets => Set<Budget>();

    public DbSet<BudgetAlert> BudgetAlerts
        => Set<BudgetAlert>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
