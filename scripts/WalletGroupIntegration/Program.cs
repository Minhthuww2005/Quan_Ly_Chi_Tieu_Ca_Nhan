using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using WalletFunding.Api;

var api = Environment.GetEnvironmentVariable("WALLET_TEST_BASE") ?? "http://127.0.0.1:5083/api";
if (!api.StartsWith("http://127.0.0.1:")) throw new Exception("Isolated localhost QA only");
var connection = Environment.GetEnvironmentVariable("WALLET_INTEGRATION_DB") ?? throw new Exception("Set WALLET_INTEGRATION_DB to the isolated QA database");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host != "127.0.0.1" || settings.Database != "htdpay_groups_qa") throw new Exception("Refusing non-QA database");
await using var source = NpgsqlDataSource.Create(connection);
using var http = new HttpClient { BaseAddress = new Uri(api + "/"), Timeout = TimeSpan.FromSeconds(30) };
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
async Task<(JsonObject Data, string Cache)> Call(string path, string token, HttpMethod? method = null, object? body = null, int status = 200)
{
    using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
    if (token != "") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body != null) request.Content = JsonContent.Create(body);
    using var response = await http.SendAsync(request);
    var raw = await response.Content.ReadAsStringAsync();
    Check((int)response.StatusCode == status, $"{method} {path}: {response.StatusCode}: {raw}");
    return (raw.Length == 0 ? [] : JsonNode.Parse(raw)!.AsObject(), response.Headers.TryGetValues("X-Group-Cache", out var h) ? h.Single() : "");
}
var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var password = "Group-Integration@123456";
async Task<(int Id, string Token, string Email)> Signup(int n)
{
    var email = $"qa-groups-live-{stamp}-{n}@example.test";
    var data = (await Call("auth/register", "", HttpMethod.Post, new { name = "Group Live QA " + n, email, phone = "07" + ((stamp % 10000000) * 10 + n).ToString("D8"), password }, 201)).Data;
    var login = (await Call("auth/login", "", HttpMethod.Post, new { identifier = email, password })).Data;
    return (J.Int(data, "userId"), J.Str(login, "token"), email);
}
var owner = await Signup(1); var member = await Signup(2); var outsider = await Signup(3); var admin = await Signup(4);
await using (var c = await source.OpenConnectionAsync())
await using (var cmd = new NpgsqlCommand("UPDATE htdpay.users SET data=jsonb_set(data,'{role}','\"admin\"') WHERE data->>'email'=@email", c))
{ cmd.Parameters.AddWithValue("email", admin.Email); Check(await cmd.ExecuteNonQueryAsync() == 1, "QA admin setup"); }
admin.Token = J.Str((await Call("admin/login", "", HttpMethod.Post, new { email = admin.Email, password })).Data, "token");
var group = (await Call("groups", owner.Token, HttpMethod.Post, new { name = "EF Redis Worker SignalR QA", budget = 100000 }, 201)).Data;
var gid = J.Int(group, "id"); var path = $"groups/{gid}";
await Call(path + "/members", owner.Token, HttpMethod.Post, new { email = member.Email });
async Task<long> Pending()
{
    await using var c = await source.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("SELECT count(*) FROM htdpay.group_outbox WHERE group_id=@id AND processed_at IS NULL", c);
    cmd.Parameters.AddWithValue("id", gid); return (long)(await cmd.ExecuteScalarAsync())!;
}
async Task Wait(Func<Task<bool>> condition, string label)
{
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    while (!await condition()) { await Task.Delay(100, limit.Token); }
    Check(true, label);
}
await Wait(async () => await Pending() == 0, "Startup outbox processed");
var received = Enumerable.Range(0, 4).Select(_ => new ConcurrentQueue<JsonElement>()).ToArray();
var clients = new List<HubConnection>();
try
{
    await using (var anonymous = new HubConnectionBuilder().WithUrl(api[..^4] + "/hubs/groups", o => { o.Transports = HttpTransportType.WebSockets; o.SkipNegotiation = true; }).Build())
    {
        var rejected = false;
        try { await anonymous.StartAsync(); } catch (Exception) { rejected = true; }
        Check(rejected, "Anonymous WebSocket rejected");
    }
    foreach (var (user, index) in new[] { (owner, 0), (member, 1), (outsider, 2), (admin, 3) })
    {
        var client = new HubConnectionBuilder().WithUrl(api[..^4] + "/hubs/groups", options => { options.AccessTokenProvider = () => Task.FromResult<string?>(user.Token); options.Transports = HttpTransportType.WebSockets; options.SkipNegotiation = true; }).Build();
        client.On<JsonElement>("GroupChanged", e => received[index].Enqueue(e));
        await client.StartAsync(); clients.Add(client); Check(client.State == HubConnectionState.Connected, "Authenticated WebSocket connected");
    }
    var entry = new { title = "Ăn trưa", amount = 120000, kind = "expense", category = "Ăn uống", date = DateTime.UtcNow.ToString("yyyy-MM-dd"), payer_id = member.Id };
    var result = (await Call(path + "/entries", member.Token, HttpMethod.Post, entry, 201)).Data;
    await Wait(() => Task.FromResult(received[0].Count > 0 && received[1].Count > 0 && received[3].Count > 0), "Owner/member/admin received event");
    await Wait(async () => await Pending() == 0, "Worker persisted completion");
    Check(received[2].IsEmpty, "Outsider must not receive events");
    Check(received[0].All(e => e.EnumerateObject().Count() == 2 && e.GetProperty("groupId").GetInt32() == gid), "Event contains identifiers only");
    var warm = await Call(path, owner.Token);
    for (var warmup = 0; warm.Cache == "BYPASS" && warmup < 5; warmup++)
    { await Task.Delay(1000); warm = await Call(path, owner.Token); }
    Check(warm.Cache != "BYPASS", "Redis becomes available after connection warmup");
    string revision;
    await using (var c = await source.OpenConnectionAsync())
    await using (var cmd = new NpgsqlCommand("SELECT data->>'report_version' FROM htdpay.expensegroups WHERE data->>'group_id'=@id", c))
    { cmd.Parameters.AddWithValue("id", gid.ToString()); revision = (string)(await cmd.ExecuteScalarAsync())!; }
    using (var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("Cache__Redis") ?? "127.0.0.1:16379"))
    {
        var key = GroupCacheNamespace.ForDatabase(connection) + $"htdpay:groups:v1:{gid}:{revision}:{DateTime.UtcNow:yyyy-MM}";
        await redis.GetDatabase().KeyDeleteAsync(key); // Only this QA group's key.
        var first = await Call(path, owner.Token); var second = await Call(path, owner.Token);
        Check(first.Cache == "MISS" && second.Cache == "HIT", $"Real Redis MISS then HIT (received {first.Cache}/{second.Cache})");
        Check(J.Money(second.Data["summary"]!.AsObject(), "expense") == 120000, "Cached aggregate matches EF data");
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync(key);
        Check(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromMinutes(1), "Real Redis key has bounded TTL");
    }
    await Call(path, outsider.Token, status: 404);
    await Call("admin/" + path, owner.Token, status: 403);
    var fresh = await Call(path + "/entries", owner.Token, HttpMethod.Post, entry with { amount = 1000 }, 201);
    var updated = await Call(path, owner.Token);
    Check(updated.Cache == "MISS" && J.Money(updated.Data["summary"]!.AsObject(), "expense") == 121000, "Commit revision invalidates cache immediately");
    await Call(path + $"/members/{member.Id}", owner.Token, HttpMethod.Delete);
    await Wait(async () => await Pending() == 0, "Member removal outbox processed");
    await Task.Delay(200);
    await Call(path + "/entries", owner.Token, HttpMethod.Post, entry with { amount = 2000, payer_id = owner.Id }, 201);
    await Wait(async () => await Pending() == 0, "Post-removal outbox processed");
    Guid afterRemoval;
    await using (var c = await source.OpenConnectionAsync())
    await using (var cmd = new NpgsqlCommand("SELECT event_id FROM htdpay.group_outbox WHERE group_id=@id ORDER BY created_at DESC LIMIT 1", c))
    { cmd.Parameters.AddWithValue("id", gid); afterRemoval = (Guid)(await cmd.ExecuteScalarAsync())!; }
    await Wait(() => Task.FromResult(received[0].Any(e => e.GetProperty("eventId").GetGuid() == afterRemoval)), "Owner received post-removal event");
    Check(!received[1].Any(e => e.GetProperty("eventId").GetGuid() == afterRemoval), "Revoked member cannot receive subsequent events");
    await Call(path, member.Token, status: 404);
    Check(received[2].IsEmpty, "No outsider events after writes");
    // A durable event without any Channel signal must still be found by polling.
    var missedSignal = Guid.NewGuid();
    await using (var c = await source.OpenConnectionAsync())
    await using (var cmd = new NpgsqlCommand("INSERT INTO htdpay.group_outbox(event_id,group_id) VALUES (@event,@group)", c))
    { cmd.Parameters.AddWithValue("event", missedSignal); cmd.Parameters.AddWithValue("group", gid); await cmd.ExecuteNonQueryAsync(); }
    await Wait(() => Task.FromResult(received[0].Any(e => e.GetProperty("eventId").GetGuid() == missedSignal)), "Periodic outbox scan recovers missed Channel signal");
    await clients[0].StopAsync(); await clients[0].StartAsync();
    Check(J.Money((await Call(path, owner.Token)).Data["summary"]!.AsObject(), "expense") == 123000, "Reconnect snapshot contains committed changes");

    // A legacy users update and an EF insert share one external transaction.
    // Force a unique-index error and prove neither side is committed.
    var rolledBack = false;
    await using (var repository = new PostgresWalletRepository(source))
    {
        await repository.Begin(true);
        (await repository.All("users")).Single(u => J.Int(u, "user_id") == owner.Id)["name"] = "MUST_ROLL_BACK";
        await repository.Add("expensegroups", J.Obj(new { group_id = gid, name = "duplicate", owner_id = owner.Id }));
        try { await repository.Commit(); } catch (DbUpdateException) { rolledBack = true; }
    }
    Check(rolledBack, "EF unique failure triggered rollback");
    Check(J.Str((await Call("auth/me", owner.Token)).Data["user"]!.AsObject(), "name") != "MUST_ROLL_BACK", "Legacy update rolled back with EF failure");

    // Real hosted worker can stop while waiting for the channel/poll timer.
    var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddSingleton(source); builder.Services.AddSingleton<GroupEventQueue>(); builder.Services.AddSignalR(); builder.Services.AddHostedService<GroupEventWorker>();
    await using var host = builder.Build(); await host.StartAsync();
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await host.StopAsync(stop.Token);
    Check(!stop.IsCancellationRequested, "Production BackgroundService graceful shutdown");
    Directory.CreateDirectory(".wallet-build");
    await File.WriteAllTextAsync(".wallet-build/group-runtime-qa.json", JsonSerializer.Serialize(new { owner.Email, password, groupId = gid }));
    Console.WriteLine(JsonSerializer.Serialize(new { pass = true, checks, efCore = "JSONB read/write and shared rollback verified", redis = "MISS/HIT and generation invalidation verified", worker = "durable outbox and graceful stop verified", signalR = "WebSocket owner/member/admin isolation and reconnect verified", groupId = gid }));
}
finally { foreach (var client in clients) await client.DisposeAsync(); }
