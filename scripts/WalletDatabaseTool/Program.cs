using Npgsql;
using System.Text.Json;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
var localAdmin = args.FirstOrDefault() == "--local-test-admin";
var settings = File.ReadAllLines(Path.Combine(root, localAdmin ? ".env" : ".env.wallet"))
    .Where(l => !l.StartsWith('#') && l.Contains('='))
    .ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]);
var connectionString = localAdmin ? "Host=localhost;Port=55432;Database=revenueflow_demo;Username=revenueflow;Password=" + settings["POSTGRES_PASSWORD"] : settings["ConnectionStrings__Database"];
await using var data = NpgsqlDataSource.Create(connectionString);
await using var connection = await data.OpenConnectionAsync();
if (args.FirstOrDefault() == "--enable-demo-admin")
{
    await using var promote = new NpgsqlCommand("UPDATE htdpay.users SET data=jsonb_set(data,'{role}','\"admin\"'::jsonb) WHERE data->>'email'='admin.demo@example.test' AND data->>'name'='Admin Demo'", connection);
    if (await promote.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Demo account not found");
    Console.WriteLine("Demo admin enabled.");
    return;
}
if (localAdmin)
{
    var email = args.ElementAtOrDefault(1) ?? "";
    if (!email.StartsWith("qa-wallet-") || !email.EndsWith("@example.test")) throw new InvalidOperationException("Only local QA accounts are permitted.");
    await using var promote = new NpgsqlCommand("UPDATE htdpay.users SET data=jsonb_set(data,'{role}','\"admin\"'::jsonb) WHERE data->>'email'=@email", connection);
    promote.Parameters.AddWithValue("email", email);
    if (await promote.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("QA account not found");
    return;
}
var tables = new List<(string Schema, string Name)>();
await using (var cmd = new NpgsqlCommand("SELECT table_schema,table_name FROM information_schema.tables WHERE table_type='BASE TABLE' AND table_schema NOT IN ('pg_catalog','information_schema') ORDER BY table_schema,table_name", connection))
await using (var reader = await cmd.ExecuteReaderAsync())
    while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
var rows = new List<object>();
foreach (var (schema, name) in tables)
{
    var quoted = '"' + schema.Replace("\"", "\"\"") + "\".\"" + name.Replace("\"", "\"\"") + '"';
    await using var cmd = new NpgsqlCommand("SELECT count(*) FROM " + quoted, connection);
    var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
    rows.Add(new { schema, table = name, count });
}
var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
var output = Path.Combine(root, "docs/wallet-test-results"); Directory.CreateDirectory(output);
await File.WriteAllTextAsync(Path.Combine(output, "neon-schema-audit.json"), json);
Console.WriteLine(json);
