using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public interface IWalletService
{
    Task<ServiceResponse> ExecuteAsync(WalletRequest context);
}
