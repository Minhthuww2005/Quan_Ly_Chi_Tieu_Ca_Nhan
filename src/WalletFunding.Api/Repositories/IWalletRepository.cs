using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletRepository : IAsyncDisposable
{
    Task Begin(bool write);
    Task<List<JsonObject>> All(string table);
    // Rows whose JSON field equals value. The default scans All(); the PostgreSQL repository pushes the
    // filter into SQL (indexed for transactions.wallet_id) so large tables are not loaded per request.
    // First row (table order) whose field equals value, tracked like All(): changes are saved on Commit.
    // The PostgreSQL repository reads just that row, so hot paths no longer load whole tables.
    async Task<JsonObject?> FindOne(string table, string field, string value, bool ignoreCase = false) =>
        (await All(table)).FirstOrDefault(x => string.Equals(J.Str(x, field), value, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    // Failed PIN/login attempts, written outside the request transaction so a rejected request still counts.
    Task<AuthAttempts> GetAttempts(string key) => Task.FromResult(new AuthAttempts(0, null));
    Task<AuthAttempts> RecordFailure(string key, int maxFailures, TimeSpan lockFor) => Task.FromResult(new AuthAttempts(1, null));
    Task ClearAttempts(string key) => Task.CompletedTask;

    // Ends the request's unit of work without saving anything (and releases the write lock).
    Task Rollback() => Task.CompletedTask;
    // Stores a money operation that a business rule rejected as a "failed" history row. It is written on its
    // own connection after Rollback, so it survives the rejected request and never changes a balance.
    Task RecordFailedTransaction(JsonObject row) => Task.CompletedTask;

    // One row read outside the request's unit of work (no cache, no transaction, no lock).
    // Only for checks whose result is re-validated inside the transaction, such as a PIN hash.
    async Task<JsonObject?> Peek(string table, string field, string value) =>
        (await All(table)).FirstOrDefault(x => J.Str(x, field) == value);
    async Task<List<JsonObject>> Where(string table, string field, string value) =>
        (await All(table)).Where(x => J.Str(x, field) == value).ToList();
    Task Add(string table, JsonObject data);
    Task<int> Next(string table, string field);
    Task<string> NextCode(string table, string prefix);
    Task Commit();
}

public sealed record AuthAttempts(int Failures, DateTimeOffset? LockedUntil)
{
    public bool Locked => LockedUntil is { } until && until > DateTimeOffset.UtcNow;
}
