using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletRepository : IAsyncDisposable
{
    Task Begin(bool write);
    Task<List<JsonObject>> All(string table);
    // Rows whose JSON field equals value. The default scans All(); the PostgreSQL repository pushes the
    // filter into SQL (indexed for transactions.wallet_id) so large tables are not loaded per request.
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
