using System.Text.Json.Nodes;
using Npgsql;
using Microsoft.AspNetCore.DataProtection;

namespace WalletFunding.Api;

public sealed class PostgresWalletRepository(NpgsqlDataSource source) : IWalletRepository, IWalletAdminReads
{
    public static readonly string[] Tables = ["users", "wallets", "transactions", "cards", "linkedbanks", "bankaccounts", "supportedbanks", "kycprofiles", "savingsaccounts", "investments", "loanaccounts", "insurances", "contacts", "notifications", "merchants", "billers", "tokens", "gatewaytransactions", "securitylogs", "payments", "personalinformations", "simpletransactions", "simplecards", "simpleinvestments", "simplesavings", "simpleinsurances", "simplemerchants", "simplebillers", "simplenotifications", "simpleloans", "simplecontacts"];
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private readonly Dictionary<string, List<JsonObject>> cache = [];
    private readonly Dictionary<string, string> originals = [];
    // Rows added to a table that this request never loaded: inserted on Commit without reading the table.
    // Rows loaded one by one (FindOne) from a table that is not fully cached; tracked for Commit like cached rows.
    private readonly Dictionary<string, Dictionary<string, JsonObject>> partial = [];
    private readonly Dictionary<string, List<JsonObject>> pending = [];
    private readonly Dictionary<string, int> allocated = [];

    public static async Task Initialize(NpgsqlDataSource source, string? importPath, IDataProtector protector)
    {
        await using var c = await source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(72491001); CREATE SCHEMA IF NOT EXISTS htdpay; CREATE TABLE IF NOT EXISTS htdpay.schema_versions (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());", c, tx)) await cmd.ExecuteNonQueryAsync();
        foreach (var table in Tables)
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
            CREATE INDEX IF NOT EXISTS ix_htd_transaction_time ON htdpay.transactions ((COALESCE(data->>'timestamp','')));
            CREATE INDEX IF NOT EXISTS ix_htd_payment_request ON htdpay.payments ((data->>'request_key'));
            CREATE INDEX IF NOT EXISTS ix_htd_payment_order ON htdpay.payments ((data->>'order_id'));
            CREATE INDEX IF NOT EXISTS ix_htd_kyc_id ON htdpay.kycprofiles ((data->>'kyc_id'));
            CREATE INDEX IF NOT EXISTS ix_htd_kyc_user ON htdpay.kycprofiles ((data->>'user_id'));
            CREATE INDEX IF NOT EXISTS ix_htd_kyc_hash ON htdpay.kycprofiles ((data->>'national_id_hash'));
            CREATE INDEX IF NOT EXISTS ix_htd_transaction_number ON htdpay.transactions ((CASE WHEN data->>'trans_id' ~ '^[0-9]{1,9}$' THEN (data->>'trans_id')::int END));
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
        await using var cmd = new NpgsqlCommand($"SELECT record_key,data::text FROM htdpay.{table}", connection!, transaction);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<JsonObject>();
        partial.Remove(table, out var tracked);
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            // A row already loaded by FindOne keeps its instance (and in-request changes) and its original snapshot.
            if (tracked != null && tracked.TryGetValue(key, out var known)) { result.Add(known); continue; }
            var d = JsonNode.Parse(reader.GetString(1))!.AsObject();
            d["_id"] = key; result.Add(d); originals[table + "/" + key] = d.ToJsonString();
        }
        if (pending.Remove(table, out var added)) result.AddRange(added);
        return cache[table] = result;
    }

    public async Task<JsonObject?> FindOne(string table, string field, string value, bool ignoreCase = false)
    {
        if (!Tables.Contains(table) || !System.Text.RegularExpressions.Regex.IsMatch(field, "^[a-zA-Z_]{1,40}$")) throw new InvalidOperationException("Unknown table or field");
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (cache.TryGetValue(table, out var loaded)) return loaded.FirstOrDefault(x => string.Equals(J.Str(x, field), value, comparison));
        var condition = ignoreCase ? $"lower(data->>'{field}') = lower(@value)" : $"data->>'{field}' = @value";
        await using (var cmd = new NpgsqlCommand($"SELECT record_key,data::text FROM htdpay.{table} WHERE {condition} ORDER BY created_at, ctid LIMIT 1", connection!, transaction))
        {
            cmd.Parameters.AddWithValue("value", value);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var rows = partial.TryGetValue(table, out var p) ? p : partial[table] = [];
                if (rows.TryGetValue(key, out var known)) return known;
                var d = JsonNode.Parse(reader.GetString(1))!.AsObject(); d["_id"] = key;
                originals[table + "/" + key] = d.ToJsonString(); rows[key] = d;
                return d;
            }
        }
        return pending.GetValueOrDefault(table)?.FirstOrDefault(x => string.Equals(J.Str(x, field), value, comparison));
    }

    // ---- admin read models (read-only; no cache, no tracking) ----
    private const string Blocked = "lower(COALESCE(u.data->>'accountStatus', u.data->>'status', 'active')) IN ('blocked','closed','frozen','suspicious','inactive','suspended')";
    // JSON numbers are cast directly (cheap); numeric strings from imported data are still accepted.
    private static string Num(string field) =>
        $"(CASE jsonb_typeof(data->'{field}') WHEN 'number' THEN (data->>'{field}')::numeric " +
        $"WHEN 'string' THEN (CASE WHEN data->>'{field}' ~ '^-?[0-9]+(\\.[0-9]+)?([eE][-+]?[0-9]+)?$' THEN (data->>'{field}')::numeric ELSE 0 END) ELSE 0 END)";

    private async Task<List<JsonObject>> Rows(string sql, Action<NpgsqlParameterCollection>? bind = null)
    {
        await using var cmd = new NpgsqlCommand(sql, connection!, transaction);
        bind?.Invoke(cmd.Parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<JsonObject>();
        while (await reader.ReadAsync())
        {
            var d = JsonNode.Parse(reader.GetString(0))!.AsObject();
            for (var i = 1; i < reader.FieldCount; i++) d[reader.GetName(i)] = reader.IsDBNull(i) ? null : JsonValue.Create(reader.GetValue(i).ToString());
            list.Add(d);
        }
        return list;
    }

    public async Task<JsonObject> AdminStats(string today)
    {
        var row = (await Rows($"""
            SELECT jsonb_build_object()::text,
              (SELECT count(*) FROM htdpay.users) AS total_users,
              (SELECT count(*) FROM htdpay.users u WHERE NOT ({Blocked})) AS active_users,
              (SELECT count(*) FROM htdpay.wallets) AS total_wallets,
              (SELECT COALESCE(sum({Num("balance")}), 0) FROM htdpay.wallets) AS total_balance,
              (SELECT count(*) FROM htdpay.transactions) AS total_transactions,
              (SELECT count(*) FROM htdpay.kycprofiles WHERE data->>'status' IN ('submitted','pending')) AS pending_kyc,
              (SELECT count(*) FROM htdpay.kycprofiles WHERE data->>'status' = 'verified') AS verified_kyc,
              (SELECT count(*) FROM htdpay.cards) AS total_cards,
              (SELECT count(*) FROM htdpay.bankaccounts) AS total_bank_accounts
            """))[0];
        return J.Obj(new
        {
            totalUsers = int.Parse(J.Str(row, "total_users")), activeUsers = int.Parse(J.Str(row, "active_users")), totalWallets = int.Parse(J.Str(row, "total_wallets")),
            totalBalance = J.Money(row, "total_balance"), totalTransactions = int.Parse(J.Str(row, "total_transactions")), pendingKYC = int.Parse(J.Str(row, "pending_kyc")),
            verifiedKYC = int.Parse(J.Str(row, "verified_kyc")), totalCards = int.Parse(J.Str(row, "total_cards")), totalBankAccounts = int.Parse(J.Str(row, "total_bank_accounts")),
        });
    }

    public async Task<(List<JsonObject> Rows, int Total)> AdminUsers(int offset, int limit)
    {
        var rows = await Rows($"""
            SELECT u.data::text, w.data->>'balance' AS _balance,
              CASE WHEN w.record_key IS NULL THEN 0 ELSE (SELECT count(*) FROM htdpay.transactions t WHERE t.data->>'wallet_id' = w.data->>'wallet_id') END AS _transaction_count,
              (SELECT k.data->>'status' FROM htdpay.kycprofiles k WHERE k.owner_id = u.owner_id ORDER BY k.created_at DESC, k.ctid DESC LIMIT 1) AS _kyc_status
            FROM htdpay.users u
            LEFT JOIN htdpay.wallets w ON w.data->>'user_id' = u.data->>'user_id'
            ORDER BY COALESCE(u.data->>'created_at', '') DESC, u.record_key
            LIMIT @limit OFFSET @offset
            """, p => { p.AddWithValue("limit", limit); p.AddWithValue("offset", offset); });
        await using var count = new NpgsqlCommand("SELECT count(*) FROM htdpay.users", connection!, transaction);
        return (rows, Convert.ToInt32(await count.ExecuteScalarAsync()));
    }

    public async Task<(List<JsonObject> Rows, int Total, JsonObject Stats)> AdminTransactions(int offset, int limit, string today)
    {
        var rows = await Rows("""
            SELECT t.data::text, u.data->>'name' AS _sender_name
            FROM (SELECT * FROM htdpay.transactions ORDER BY COALESCE(data->>'timestamp', '') DESC, record_key LIMIT @limit OFFSET @offset) t
            LEFT JOIN htdpay.wallets w ON w.data->>'wallet_id' = t.data->>'wallet_id'
            LEFT JOIN htdpay.users u ON u.data->>'user_id' = w.data->>'user_id'
            ORDER BY COALESCE(t.data->>'timestamp', '') DESC, t.record_key
            """, p => { p.AddWithValue("limit", limit); p.AddWithValue("offset", offset); });
        var amount = Num("amount"); var fee = Num("fee");
        var stats = (await Rows($"""
            SELECT jsonb_build_object()::text,
              count(*) AS total,
              count(*) FILTER (WHERE data->>'status' IN ('success','completed')) AS completed,
              count(*) FILTER (WHERE data->>'status' = 'pending') AS pending,
              count(*) FILTER (WHERE data->>'status' = 'failed') AS failed,
              COALESCE(sum(-{amount}) FILTER (WHERE data->>'status' IN ('success','completed') AND {amount} < 0), 0) AS total_volume,
              COALESCE(sum({fee}) FILTER (WHERE data->>'status' IN ('success','completed')), 0) AS total_fees,
              count(*) FILTER (WHERE data->>'timestamp' LIKE @today || '%') AS today_transactions,
              COALESCE(sum(-{amount}) FILTER (WHERE data->>'status' IN ('success','completed') AND data->>'timestamp' LIKE @today || '%' AND {amount} < 0), 0) AS today_volume
            FROM htdpay.transactions
            """, p => p.AddWithValue("today", today)))[0];
        return (rows, int.Parse(J.Str(stats, "total")), J.Obj(new
        {
            total = int.Parse(J.Str(stats, "total")), completed = int.Parse(J.Str(stats, "completed")), pending = int.Parse(J.Str(stats, "pending")), failed = int.Parse(J.Str(stats, "failed")),
            totalVolume = J.Money(stats, "total_volume"), totalFees = J.Money(stats, "total_fees"), todayTransactions = int.Parse(J.Str(stats, "today_transactions")), todayVolume = J.Money(stats, "today_volume"),
        }));
    }

    public async Task<List<JsonObject>> AdminKyc(bool pendingOnly) => await Rows($"""
        SELECT (k.data - 'front_image' - 'back_image' - 'face_image')::text, u.data->>'name' AS _user_name, u.data->>'email' AS _user_email,
               (k.data ? 'front_image' OR k.data ? 'back_image' OR k.data ? 'face_image') AS _has_images
        FROM htdpay.kycprofiles k
        LEFT JOIN htdpay.users u ON u.data->>'user_id' = k.data->>'user_id'
        {(pendingOnly ? "WHERE k.data->>'status' IN ('pending','submitted')" : "")}
        ORDER BY k.created_at, k.ctid
        """);

    // ---- failed authentication attempts (own connection, autocommit) ----
    private static int attemptsTableReady;
    private async Task<NpgsqlConnection> AttemptsConnection()
    {
        var c = await source.OpenConnectionAsync();
        if (Volatile.Read(ref attemptsTableReady) == 0)
        {
            await using var ddl = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS htdpay; CREATE TABLE IF NOT EXISTS htdpay.authattempts (key text PRIMARY KEY, failures int NOT NULL, locked_until timestamptz, updated_at timestamptz NOT NULL)", c);
            await ddl.ExecuteNonQueryAsync(); Volatile.Write(ref attemptsTableReady, 1);
        }
        return c;
    }
    private static async Task<AuthAttempts> ReadAttempts(NpgsqlCommand cmd)
    {
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return new AuthAttempts(0, null);
        return new AuthAttempts(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }
    public async Task<AuthAttempts> GetAttempts(string key)
    {
        await using var c = await AttemptsConnection();
        await using var cmd = new NpgsqlCommand("SELECT failures, locked_until FROM htdpay.authattempts WHERE key=@k", c);
        cmd.Parameters.AddWithValue("k", key);
        return await ReadAttempts(cmd);
    }
    public async Task<AuthAttempts> RecordFailure(string key, int maxFailures, TimeSpan lockFor)
    {
        await using var c = await AttemptsConnection();
        // Count restarts after an expired lock or after the lock window passes without new failures.
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO htdpay.authattempts AS a (key, failures, locked_until, updated_at)
            VALUES (@k, 1, CASE WHEN 1 >= @max THEN now() + @lock END, now())
            ON CONFLICT (key) DO UPDATE SET
              failures = CASE WHEN a.locked_until <= now() OR a.updated_at < now() - @lock THEN 1 ELSE a.failures + 1 END,
              locked_until = CASE WHEN (CASE WHEN a.locked_until <= now() OR a.updated_at < now() - @lock THEN 1 ELSE a.failures + 1 END) >= @max
                                  THEN now() + @lock ELSE NULL END,
              updated_at = now()
            RETURNING failures, locked_until
            """, c);
        cmd.Parameters.AddWithValue("k", key); cmd.Parameters.AddWithValue("max", maxFailures); cmd.Parameters.AddWithValue("lock", lockFor);
        return await ReadAttempts(cmd);
    }
    public async Task ClearAttempts(string key)
    {
        await using var c = await AttemptsConnection();
        await using var cmd = new NpgsqlCommand("DELETE FROM htdpay.authattempts WHERE key=@k", c);
        cmd.Parameters.AddWithValue("k", key); await cmd.ExecuteNonQueryAsync();
    }

    public async Task<JsonObject?> Peek(string table, string field, string value)
    {
        if (!Tables.Contains(table) || !System.Text.RegularExpressions.Regex.IsMatch(field, "^[a-z_]{1,40}$")) throw new InvalidOperationException("Unknown table or field");
        // Separate pooled connection: runs before Begin, never joins the request transaction or the advisory lock.
        await using var peekConnection = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand($"SELECT data::text FROM htdpay.{table} WHERE data->>'{field}' = @value LIMIT 1", peekConnection);
        cmd.Parameters.AddWithValue("value", value);
        return await cmd.ExecuteScalarAsync() is string json ? JsonNode.Parse(json)!.AsObject() : null;
    }

    public async Task<List<JsonObject>> Where(string table, string field, string value)
    {
        if (!Tables.Contains(table) || !System.Text.RegularExpressions.Regex.IsMatch(field, "^[a-z_]{1,40}$")) throw new InvalidOperationException("Unknown table or field");
        if (cache.TryGetValue(table, out var loaded)) return loaded.Where(x => J.Str(x, field) == value).ToList();
        var tracked = partial.GetValueOrDefault(table);
        // Field name is whitelisted above and written literally so the expression index can be used.
        await using var cmd = new NpgsqlCommand($"SELECT record_key,data::text FROM htdpay.{table} WHERE data->>'{field}' = @value ORDER BY created_at, ctid", connection!, transaction);
        cmd.Parameters.AddWithValue("value", value);
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<JsonObject>();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            if (tracked != null && tracked.TryGetValue(key, out var known)) { result.Add(known); continue; }
            var d = JsonNode.Parse(reader.GetString(1))!.AsObject(); d["_id"] = key; result.Add(d);
        }
        // Read-only snapshot: not cached, so Commit never treats unloaded rows as deleted.
        if (pending.TryGetValue(table, out var added)) result.AddRange(added.Where(x => J.Str(x, field) == value));
        return result;
    }

    public async Task Add(string table, JsonObject data)
    {
        data["_id"] ??= JsonValue.Create(Guid.NewGuid().ToString("N"));
        if (cache.TryGetValue(table, out var loaded)) { loaded.Add(data); return; }
        if (!Tables.Contains(table)) throw new InvalidOperationException("Unknown table");
        (pending.TryGetValue(table, out var list) ? list : pending[table] = []).Add(data);
        await Task.CompletedTask;
    }

    // MAX+1 is computed in SQL instead of loading every row; safe because writers hold the advisory lock.
    public async Task<int> Next(string table, string field)
    {
        if (cache.TryGetValue(table, out var loaded)) return loaded.Select(x => J.Int(x, field)).DefaultIfEmpty().Max() + 1;
        if (!Tables.Contains(table) || !System.Text.RegularExpressions.Regex.IsMatch(field, "^[a-z_]{1,40}$")) throw new InvalidOperationException("Unknown table or field");
        var slot = table + "/" + field;
        if (!allocated.TryGetValue(slot, out var last))
        {
            await using var cmd = new NpgsqlCommand($"SELECT COALESCE(max(CASE WHEN data->>'{field}' ~ '^[0-9]{{1,9}}$' THEN (data->>'{field}')::int END), 0) FROM htdpay.{table}", connection!, transaction);
            last = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        last = Math.Max(last, pending.GetValueOrDefault(table)?.Select(x => J.Int(x, field)).DefaultIfEmpty().Max() ?? 0);
        allocated[slot] = last + 1;
        return last + 1;
    }
    public async Task<string> NextCode(string table, string prefix) => prefix + ((await All(table)).Select(x => int.TryParse(J.Str(x, "id").Replace(prefix, ""), out var n) ? n : 0).DefaultIfEmpty().Max() + 1).ToString("D6");

    public async Task Commit()
    {
        if (transaction == null) return;
        foreach (var (table, docs) in pending)
            foreach (var d in docs)
            {
                await using var insert = new NpgsqlCommand($"INSERT INTO htdpay.{table}(record_key,data) VALUES (@id,@data::jsonb)", connection!, transaction);
                insert.Parameters.AddWithValue("id", J.Str(d, "_id")); insert.Parameters.AddWithValue("data", d.ToJsonString()); await insert.ExecuteNonQueryAsync();
            }
        foreach (var (table, docs) in cache)
        {
            var keys = docs.Select(d => J.Str(d, "_id")).ToHashSet();
            foreach (var old in originals.Keys.Where(k => k.StartsWith(table + "/")).ToArray())
                if (!keys.Contains(old[(table.Length + 1)..]))
                {
                    await using var del = new NpgsqlCommand($"DELETE FROM htdpay.{table} WHERE record_key=@id", connection!, transaction);
                    del.Parameters.AddWithValue("id", old[(table.Length + 1)..]); await del.ExecuteNonQueryAsync();
                }
            foreach (var d in docs)
            {
                var key = J.Str(d, "_id"); var json = d.ToJsonString();
                if (originals.GetValueOrDefault(table + "/" + key) == json) continue;
                await using var cmd = new NpgsqlCommand($"INSERT INTO htdpay.{table}(record_key,data) VALUES (@id,@data::jsonb) ON CONFLICT(record_key) DO UPDATE SET data=excluded.data", connection!, transaction);
                cmd.Parameters.AddWithValue("id", key); cmd.Parameters.AddWithValue("data", json); await cmd.ExecuteNonQueryAsync();
            }
        }
        foreach (var (table, rows) in partial)
            foreach (var (key, d) in rows)
            {
                var json = d.ToJsonString();
                if (originals.GetValueOrDefault(table + "/" + key) == json) continue;
                await using var cmd = new NpgsqlCommand($"UPDATE htdpay.{table} SET data=@data::jsonb WHERE record_key=@id", connection!, transaction);
                cmd.Parameters.AddWithValue("id", key); cmd.Parameters.AddWithValue("data", json); await cmd.ExecuteNonQueryAsync();
            }
        await transaction.CommitAsync();
    }

    public async Task Rollback()
    {
        if (transaction != null) { await transaction.RollbackAsync(); await transaction.DisposeAsync(); transaction = null; }
        cache.Clear(); originals.Clear(); pending.Clear(); partial.Clear(); allocated.Clear();
    }

    public async Task RecordFailedTransaction(JsonObject row)
    {
        await using var c = await source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Same lock as every write, so the trans_id below cannot collide with a concurrent transfer.
        await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(72491001)", c, tx)) await lockCmd.ExecuteNonQueryAsync();
        await using (var next = new NpgsqlCommand("SELECT COALESCE(max(CASE WHEN data->>'trans_id' ~ '^[0-9]{1,9}$' THEN (data->>'trans_id')::int END), 0) + 1 FROM htdpay.transactions", c, tx))
            row["trans_id"] = Convert.ToInt32(await next.ExecuteScalarAsync());
        row["_id"] ??= JsonValue.Create(Guid.NewGuid().ToString("N"));
        await using (var insert = new NpgsqlCommand("INSERT INTO htdpay.transactions(record_key,data) VALUES (@id,@data::jsonb)", c, tx))
        {
            insert.Parameters.AddWithValue("id", J.Str(row, "_id")); insert.Parameters.AddWithValue("data", row.ToJsonString());
            await insert.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction != null) await transaction.DisposeAsync();
        if (connection != null) await connection.DisposeAsync();
    }
}

