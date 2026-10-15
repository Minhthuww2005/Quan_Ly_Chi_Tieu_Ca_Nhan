using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController]
[Route("api/savings")]
[Route("api/investments")]
[Route("api/loans")]
[Route("api/insurance")]
[EnableRateLimiting("api")]
public sealed class FinanceController(FinanceService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();
}
