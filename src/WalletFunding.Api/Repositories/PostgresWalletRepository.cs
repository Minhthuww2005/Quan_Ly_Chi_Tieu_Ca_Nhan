using System.Text.Json.Nodes;
using Npgsql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace WalletFunding.Api;

public sealed class PostgresWalletRepository(NpgsqlDataSource source, GroupEventQueue? events = null) : IWalletRepository
{
    public static readonly string[] Tables = ["users", "wallets", "transactions", "cards", "linkedbanks", "bankaccounts", "supportedbanks", "kycprofiles", "savingsaccounts", "investments", "loanaccounts", "insurances", "contacts", "notifications", "merchants", "billers", "tokens", "gatewaytransactions", "securitylogs", "payments", "personalinformations", "simpletransactions", "simplecards", "simpleinvestments", "simplesavings", "simpleinsurances", "simplemerchants", "simplebillers", "simplenotifications", "simpleloans", "simplecontacts", "expensegroups", "expensemembers", "expenseentries"];
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private readonly Dictionary<string, List<JsonObject>> cache = [];
    private readonly Dictionary<string, string> originals = [];
    private GroupExpensesDbContext? groupDb;

    public static async Task InitializeGroupExpenses(NpgsqlDataSource source)
    {
        await using var c = await source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(72491001);
            CREATE SCHEMA IF NOT EXISTS htdpay;
            CREATE TABLE IF NOT EXISTS htdpay.schema_versions (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS htdpay.expensegroups (record_key text PRIMARY KEY, data jsonb NOT NULL CHECK(jsonb_typeof(data)='object'), created_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS htdpay.expensemembers (record_key text PRIMARY KEY, data jsonb NOT NULL CHECK(jsonb_typeof(data)='object'), created_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS htdpay.expenseentries (record_key text PRIMARY KEY, data jsonb NOT NULL CHECK(jsonb_typeof(data)='object'), created_at timestamptz NOT NULL DEFAULT now());
            CREATE UNIQUE INDEX IF NOT EXISTS ux_expensegroups_id ON htdpay.expensegroups ((data->>'group_id'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_expensemembers_user ON htdpay.expensemembers ((data->>'group_id'), (data->>'user_id'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_expenseentries_id ON htdpay.expenseentries ((data->>'entry_id'));
            CREATE INDEX IF NOT EXISTS ix_expenseentries_group_date ON htdpay.expenseentries ((data->>'group_id'), (data->>'date'));
            CREATE TABLE IF NOT EXISTS htdpay.group_outbox (
                event_id uuid PRIMARY KEY, group_id integer NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(), processed_at timestamptz,
                attempts integer NOT NULL DEFAULT 0, next_attempt_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_group_outbox_pending ON htdpay.group_outbox(next_attempt_at) WHERE processed_at IS NULL;
            INSERT INTO htdpay.schema_versions(version) VALUES (2) ON CONFLICT DO NOTHING;
            INSERT INTO htdpay.schema_versions(version) VALUES (3) ON CONFLICT DO NOTHING;
            """, c, tx);
        await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    public static async Task Initialize(NpgsqlDataSource source, string? importPath, IDataProtector protector)
    {
        await using var c = await source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(72491001); CREATE SCHEMA IF NOT EXISTS htdpay; CREATE TABLE IF NOT EXISTS htdpay.schema_versions (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());", c, tx)) await cmd.ExecuteNonQueryAsync();
        // Group tables have their own additive schema and indexes; they do not use
        // the legacy wallet collection's generated owner_id column.
        foreach (var table in Tables.Where(t => t is not ("expensegroups" or "expensemembers" or "expenseentries")))
        {
            var sql = $"""
                CREATE TABLE IF NOT EXISTS htdpay.{table} (
                    record_key text PRIMARY KEY,
                    data jsonb NOT NULL CHECK (jsonb_typeof(data) = 'object'),
                    owner_id bigint GENERATED ALWAYS AS (NULLIF(COALESCE(data->>'user_id',data->>'ownerId'),'')::bigint) STORED,
                    created_at timestamptz NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ix_{table}_owner ON htdpay.{table}(owner_id);
                """;
            await using var cmd = new NpgsqlCommand(sql, c, tx); await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = new NpgsqlCommand("""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_user_id ON htdpay.users ((data->>'user_id'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_email ON htdpay.users (lower(data->>'email'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_phone ON htdpay.users ((data->>'phone')) WHERE COALESCE(data->>'phone','') <> '';
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_wallet_user ON htdpay.wallets ((data->>'user_id'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_wallet_id ON htdpay.wallets ((data->>'wallet_id'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_transaction_id ON htdpay.transactions ((data->>'trans_id'));
            CREATE INDEX IF NOT EXISTS ix_htd_transaction_wallet ON htdpay.transactions ((data->>'wallet_id'), (data->>'timestamp'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_bank_code ON htdpay.supportedbanks ((data->>'bank_code'));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_htd_bank_account ON htdpay.bankaccounts ((data->>'bank_code'), (data->>'account_number'));
            INSERT INTO htdpay.schema_versions(version) VALUES (1) ON CONFLICT DO NOTHING;
            """, c, tx)) await cmd.ExecuteNonQueryAsync();
        if (!string.IsNullOrEmpty(importPath))
        {
            var export = JsonNode.Parse(await File.ReadAllTextAsync(importPath))!.AsObject();
            foreach (var (table, items) in export)
            {
                if (!Tables.Contains(table) || items is not JsonArray array) continue;
                foreach (var item in array)
                {
                    var data = item!.DeepClone().AsObject();
                    NormalizeExtendedJson(data);
                    foreach (var field in new[] { "national_id_encrypted", "date_of_birth_encrypted", "address_encrypted", "phone_encrypted", "document_number_encrypted", "card_number_encrypted" })
                    {
                        var value = J.Str(data, field);
                        if (value.Length > 0 && !value.Contains(':') && !value.StartsWith("CfDJ")) data[field] = protector.Protect(value);
                    }
                    if (table == "simplecards" && data.ContainsKey("card_number"))
                    {
                        var number = J.Str(data, "card_number");
                        data["card_number_encrypted"] = protector.Protect(number);
                        data["last4_digits"] = number[^Math.Min(4, number.Length)..];
                        data.Remove("card_number");
                    }
                    if (table == "users")
                    {
                        data["status"] ??= data["accountStatus"]?.DeepClone() ?? JsonValue.Create("active");
                        data["accountStatus"] ??= data["status"]!.DeepClone();
                    }
                    await using var cmd = new NpgsqlCommand($"INSERT INTO htdpay.{table}(record_key,data) VALUES (@id,@data::jsonb) ON CONFLICT(record_key) DO NOTHING", c, tx);
                    cmd.Parameters.AddWithValue("id", data["_id"]?.ToString() ?? Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("data", data.ToJsonString()); await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        await tx.CommitAsync();
    }

    private static void NormalizeExtendedJson(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var (key, value) in obj.ToArray())
            {
                if (value is JsonObject v && v.Count == 1 && (v.ContainsKey("$oid") || v.ContainsKey("$date"))) obj[key] = (v["$oid"] ?? v["$date"])!.DeepClone();
                else if (value != null) NormalizeExtendedJson(value);
            }
        else if (node is JsonArray arr) foreach (var v in arr) if (v != null) NormalizeExtendedJson(v);
    }

    public async Task Begin(bool write)
    {
        connection = await source.OpenConnectionAsync();
        if (write)
        {
            transaction = await connection.BeginTransactionAsync();
            // Every wallet write uses the same database lock, including imports and multiple hosts.
            await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(72491001)", connection, transaction);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<JsonObject>> All(string table)
    {
        if (!Tables.Contains(table)) throw new InvalidOperationException("Unknown table");
        if (cache.TryGetValue(table, out var found)) return found;
        if (IsGroupTable(table))
        {
            var db = await GroupDb();
            var docs = table switch
            {
                "expensegroups" => await ReadGroups<GroupTable>(db),
                "expensemembers" => await ReadGroups<MemberTable>(db),
                _ => await ReadGroups<EntryTable>(db)
            };
            foreach (var doc in docs) originals[table + "/" + J.Str(doc, "_id")] = doc.ToJsonString();
            return cache[table] = docs;
        }
        await using var cmd = new NpgsqlCommand($"SELECT record_key,data::text FROM htdpay.{table}", connection!, transaction);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<JsonObject>();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0); var json = reader.GetString(1); var d = JsonNode.Parse(json)!.AsObject();
            d["_id"] = key; result.Add(d); originals[table + "/" + key] = d.ToJsonString();
        }
        return cache[table] = result;
    }

    public async Task Add(string table, JsonObject data)
    {
        data["_id"] ??= JsonValue.Create(Guid.NewGuid().ToString("N"));
        (await All(table)).Add(data);
    }

    public async Task<int> Next(string table, string field) => (await All(table)).Select(x => J.Int(x, field)).DefaultIfEmpty().Max() + 1;
    public async Task<string> NextCode(string table, string prefix) => prefix + ((await All(table)).Select(x => int.TryParse(J.Str(x, "id").Replace(prefix, ""), out var n) ? n : 0).DefaultIfEmpty().Max() + 1).ToString("D6");

    public async Task Commit()
    {
        if (transaction == null) return;
        var changedGroups = cache.Where(p => IsGroupTable(p.Key))
            .SelectMany(p => p.Value.Where(d => originals.GetValueOrDefault(p.Key + "/" + J.Str(d, "_id")) != d.ToJsonString()))
            .Select(d => J.Int(d, "group_id")).Where(id => id > 0).Distinct().ToArray();
        // The report generation changes inside the same transaction as entries.
        // An older cache key can never be reused after a committed write.
        if (changedGroups.Length > 0)
            foreach (var group in await All("expensegroups"))
                if (changedGroups.Contains(J.Int(group, "group_id"))) group["report_version"] = J.Int(group, "report_version") + 1;
        foreach (var (table, docs) in cache)
        {
            var keys = docs.Select(d => J.Str(d, "_id")).ToHashSet();
            foreach (var old in originals.Keys.Where(k => k.StartsWith(table + "/")).ToArray())
                if (!keys.Contains(old[(table.Length + 1)..]))
                {
                    if (IsGroupTable(table)) { TrackGroup(table, old[(table.Length + 1)..], "{}", EntityState.Deleted); continue; }
                    await using var del = new NpgsqlCommand($"DELETE FROM htdpay.{table} WHERE record_key=@id", connection!, transaction);
                    del.Parameters.AddWithValue("id", old[(table.Length + 1)..]); await del.ExecuteNonQueryAsync();
                }
            foreach (var d in docs)
            {
                var key = J.Str(d, "_id"); var json = d.ToJsonString();
                if (originals.GetValueOrDefault(table + "/" + key) == json) continue;
                if (IsGroupTable(table))
                {
                    TrackGroup(table, key, json, originals.ContainsKey(table + "/" + key) ? EntityState.Modified : EntityState.Added);
                    continue;
                }
                await using var cmd = new NpgsqlCommand($"INSERT INTO htdpay.{table}(record_key,data) VALUES (@id,@data::jsonb) ON CONFLICT(record_key) DO UPDATE SET data=excluded.data", connection!, transaction);
                cmd.Parameters.AddWithValue("id", key); cmd.Parameters.AddWithValue("data", json); await cmd.ExecuteNonQueryAsync();
            }
        }
        if (groupDb != null) await groupDb.SaveChangesAsync();
        foreach (var id in changedGroups)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO htdpay.group_outbox(event_id,group_id) VALUES (@event,@group)", connection!, transaction);
            cmd.Parameters.AddWithValue("event", Guid.NewGuid()); cmd.Parameters.AddWithValue("group", id); await cmd.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        foreach (var id in changedGroups) events?.Signal(id);
    }

    private static bool IsGroupTable(string table) => table is "expensegroups" or "expensemembers" or "expenseentries";
    private async Task<GroupExpensesDbContext> GroupDb()
    {
        if (groupDb != null) return groupDb;
        groupDb = new GroupExpensesDbContext(new DbContextOptionsBuilder<GroupExpensesDbContext>().UseNpgsql(connection!, contextOwnsConnection: false).Options);
        if (transaction != null) await groupDb.Database.UseTransactionAsync(transaction);
        return groupDb;
    }
    private static async Task<List<JsonObject>> ReadGroups<T>(GroupExpensesDbContext db) where T : class
    {
        var rows = await db.Set<GroupDocument<T>>().AsNoTracking().ToListAsync();
        return rows.Select(row => { var d = JsonNode.Parse(row.Data)!.AsObject(); d["_id"] = row.Key; return d; }).ToList();
    }
    private void TrackGroup(string table, string key, string json, EntityState state)
    {
        switch (table)
        {
            case "expensegroups": Track<GroupTable>(key, json, state); break;
            case "expensemembers": Track<MemberTable>(key, json, state); break;
            case "expenseentries": Track<EntryTable>(key, json, state); break;
        }
    }
    private void Track<T>(string key, string json, EntityState state) where T : class =>
        groupDb!.Entry(new GroupDocument<T> { Key = key, Data = json }).State = state;

    public async ValueTask DisposeAsync()
    {
        if (groupDb != null) await groupDb.DisposeAsync();
        if (transaction != null) await transaction.DisposeAsync();
        if (connection != null) await connection.DisposeAsync();
    }
}

