using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletRepository : IAsyncDisposable
{
    Task Begin(bool write);
    Task<List<JsonObject>> All(string table);
    // Rows whose JSON field equals value. The default scans All(); the PostgreSQL repository pushes the
    // filter into SQL (indexed for transactions.wallet_id) so large tables are not loaded per request.
    async Task<List<JsonObject>> Where(string table, string field, string value) =>
        (await All(table)).Where(x => J.Str(x, field) == value).ToList();
    Task Add(string table, JsonObject data);
    Task<int> Next(string table, string field);
    Task<string> NextCode(string table, string prefix);
    Task Commit();
}
