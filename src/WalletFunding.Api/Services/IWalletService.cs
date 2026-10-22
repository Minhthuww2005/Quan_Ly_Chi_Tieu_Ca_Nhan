using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletService
{
    Task<ServiceResponse> ExecuteAsync(WalletRequest context);
    // Runs BEFORE the repository opens its write transaction/advisory lock, so slow external
    // calls (market prices) never hold the global lock. Default: nothing to fetch.
    Task PrefetchAsync(WalletRequest context) => Task.CompletedTask;
}
