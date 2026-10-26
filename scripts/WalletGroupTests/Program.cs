using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WalletFunding.Api;

var store = new MemoryStore();
foreach (var id in new[] { 1, 2, 3, 4 }) await store.Add("users", J.Obj(new { user_id = id, name = "QA " + id, email = $"qa{id}@example.test", role = id == 4 ? "admin" : "user", accountStatus = "active" }));
var protection = new EphemeralDataProtectionProvider();
var count = 0;
async Task<JsonObject> Call(int user, string method, string path, object? body = null, int status = 200, string month = "")
{
    var snapshot = store.Snapshot();
    var claims = user == 0 ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, user == 4 ? "admin" : "user") }, "Test"));
    var service = new GroupExpenseService(store, new ConfigurationBuilder().Build(), protection, new HttpFactory(), new EnvironmentStub());
    try
    {
        var result = await service.ExecuteAsync(new WalletRequest(method, path, body == null ? [] : J.Obj(body), new RequestValues(new Dictionary<string, string> { ["month"] = month }), new RequestValues(new Dictionary<string, string>()), claims));
        if (result.StatusCode != status) throw new Exception($"Expected {status}, got {result.StatusCode}: {method} {path}");
        count++; return result.Data;
    }
    catch (ApiError e) when (e.Status == status) { store.Restore(snapshot); count++; return new JsonObject(); }
}
void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
await Call(0, "GET", "/api/groups", status: 401);
await Call(1, "POST", "/api/groups", new { name = "", budget = 100 }, 400);
await Call(1, "POST", "/api/groups", new { name = "Invalid", budget = "abc" }, 400);
await Call(1, "POST", "/api/groups", new { name = "Nhóm QA", budget = 100 }, 201);
await Call(2, "GET", "/api/groups/1", status: 404);
Check((await Call(2, "GET", "/api/groups"))["groups"]!.AsArray().Count == 0, "Leaked group list");
await Call(2, "POST", "/api/groups/1/members", new { email = "qa2@example.test" }, 404);
await Call(1, "POST", "/api/groups/1/members", new { email = "qa2@example.test" });
await Call(1, "POST", "/api/groups/1/members", new { email = "qa2@example.test" }, 409);
await Call(2, "GET", "/api/groups/1");
await Call(2, "PATCH", "/api/groups/1", new { name = "No", budget = 5, version = 1 }, 403);
await Call(2, "POST", "/api/groups/1/members", new { email = "qa3@example.test" }, 403);
await Call(2, "POST", "/api/groups/1/entries", new { title = "Bữa trưa", amount = 150, kind = "expense", category = "Ăn uống", date, payer_id = 2 }, 201);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Quỹ chung", amount = 300, kind = "income", category = "Quỹ", date }, 201);
var detail = await Call(1, "GET", "/api/groups/1");
Check(J.Money(detail["summary"]!.AsObject(), "expense") == 150 && J.Money(detail["summary"]!.AsObject(), "income") == 300 && J.Bool(detail["summary"]!.AsObject(), "overBudget"), "Wrong monthly summary/budget");
await Call(3, "GET", "/api/groups/1", status: 404);
await Call(3, "DELETE", "/api/groups/1/entries/1", new { version = 1 }, 404);
await Call(2, "DELETE", "/api/groups/1/entries/2", new { version = 1 }, 403);
await Call(2, "PUT", "/api/groups/1/entries/1", new { title = "Sửa", amount = 80, kind = "expense", category = "Ăn uống", date, version = 1 });
await Call(2, "PUT", "/api/groups/1/entries/1", new { title = "Stale", amount = 99, kind = "expense", category = "Ăn uống", date, version = 1 }, 409);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Bad", amount = 1, kind = "expense", category = "X", date, payer_id = 3 }, 400);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Bad", amount = 0, kind = "expense", category = "X", date }, 400);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Bad", amount = 1.5, kind = "expense", category = "X", date }, 400);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Bad", amount = 10, kind = "other", category = "X", date }, 400);
await Call(1, "POST", "/api/groups/1/entries", new { title = "Bad", amount = 10, kind = "expense", category = "X", date = "2099-01-01" }, 400);
await Call(1, "GET", "/api/groups/1", status: 400, month: "bad");
var past = await Call(1, "GET", "/api/groups/1", month: "2000-01");
Check(past["entries"]!.AsArray().Count == 0, "Month filtering failed");
await Call(1, "DELETE", "/api/groups/1/members/1", status: 400);
await Call(1, "DELETE", "/api/groups/1/members/2");
await Call(2, "GET", "/api/groups/1", status: 404);
await Call(1, "POST", "/api/groups/1/members", new { email = "qa2@example.test" });
await Call(2, "GET", "/api/groups/1");
await Call(2, "GET", "/api/admin/groups", status: 403);
Check((await Call(4, "GET", "/api/admin/groups"))["groups"]!.AsArray().Count == 1, "Admin list missing");
await Call(4, "GET", "/api/admin/groups/1");
await Call(4, "PATCH", "/api/admin/groups/1/status", new { archived = true, version = 1 });
await Call(1, "POST", "/api/groups/1/entries", new { title = "Closed", amount = 10, kind = "expense", category = "X", date }, 409);
await Call(1, "GET", "/api/groups/1");
await Call(4, "PATCH", "/api/admin/groups/1/status", new { archived = false, version = 1 }, 409);
await Call(4, "PATCH", "/api/admin/groups/1/status", new { archived = false, version = 2 });
await Call(4, "DELETE", "/api/admin/groups/1/entries/1", new { version = 2 }, 404);
await Call(1, "DELETE", "/api/groups/1/entries/1", new { version = 2 });
var final = await Call(1, "GET", "/api/groups/1");
Check(final["entries"]!.AsArray().Count == 1 && J.Money(final["summary"]!.AsObject(), "expense") == 0, "Soft delete/summary failed");
Check((await store.All("securitylogs")).Count == 2, "Admin status audit missing");
Check((await store.All("wallets")).Count == 0 && (await store.All("transactions")).Count == 0, "Ledger changed personal wallet");
Console.WriteLine($"PASS: {count} group service checks (memory repository, no external database).");

sealed class MemoryStore : IWalletRepository
{
    Dictionary<string, List<JsonObject>> data = [];
    public Dictionary<string, List<JsonObject>> Snapshot() => data.ToDictionary(x => x.Key, x => x.Value.Select(v => v.DeepClone().AsObject()).ToList());
    public void Restore(Dictionary<string, List<JsonObject>> snapshot) => data = snapshot;
    public Task Begin(bool write) => Task.CompletedTask;
    public Task Commit() => Task.CompletedTask;
    public Task<List<JsonObject>> All(string table) { if (!data.ContainsKey(table)) data[table] = []; return Task.FromResult(data[table]); }
    public async Task Add(string table, JsonObject value) => (await All(table)).Add(value);
    public async Task<int> Next(string table, string field) => (await All(table)).Select(x => J.Int(x, field)).DefaultIfEmpty().Max() + 1;
    public Task<string> NextCode(string table, string prefix) => throw new NotImplementedException();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
sealed class HttpFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
sealed class EnvironmentStub : IHostEnvironment { public string EnvironmentName { get; set; } = "Testing"; public string ApplicationName { get; set; } = "GroupTests"; public string ContentRootPath { get; set; } = "."; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
