using Microsoft.EntityFrameworkCore;

namespace WalletFunding.Api;

// Map the existing JSONB records without rebuilding or replacing wallet tables.
public sealed class GroupDocument<T> where T : class
{
    public string Key { get; set; } = "";
    public string Data { get; set; } = "{}";
}
public sealed class GroupTable;
public sealed class MemberTable;
public sealed class EntryTable;

public sealed class GroupExpensesDbContext(DbContextOptions<GroupExpensesDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        Map<GroupTable>(builder, "expensegroups");
        Map<MemberTable>(builder, "expensemembers");
        Map<EntryTable>(builder, "expenseentries");
    }
    private static void Map<T>(ModelBuilder builder, string table) where T : class
    {
        var entity = builder.Entity<GroupDocument<T>>();
        entity.ToTable(table, "htdpay");
        entity.HasKey(x => x.Key);
        entity.Property(x => x.Key).HasColumnName("record_key");
        entity.Property(x => x.Data).HasColumnName("data").HasColumnType("jsonb");
    }
}
