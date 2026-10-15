using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController]
[Route("api/bank")]
[Route("api/banks")]
[Route("api/bank-accounts")]
[Route("api/cards")]
[EnableRateLimiting("api")]
public sealed class BankingController(BankingService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();
}
