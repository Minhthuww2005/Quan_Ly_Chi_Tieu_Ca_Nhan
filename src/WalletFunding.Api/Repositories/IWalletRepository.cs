using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletRepository : IAsyncDisposable
{
    Task Begin(bool write);
    Task<List<JsonObject>> All(string table);
    Task Add(string table, JsonObject data);
    Task<int> Next(string table, string field);
    Task<string> NextCode(string table, string prefix);
    Task Commit();
}
