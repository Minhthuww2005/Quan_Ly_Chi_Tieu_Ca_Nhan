using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public sealed class WalletService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        await Prepare(context);
        var data = await WalletRead();
        return new ServiceResponse(data, StatusCode);
    }
    private async Task<JsonObject> WalletRead()
    {
        Require(Method == "GET" && int.TryParse(Action, out _), "Endpoint không tồn tại", 404);
        var id = int.Parse(Action); Own(id); return J.Obj(new { success = true, wallet = J.Public(await Wallet(id)) });
    }

}
