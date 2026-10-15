using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController]
[Route("api/contacts")]
[Route("api/notifications")]
[Route("api/merchants")]
[Route("api/webhook")]
[EnableRateLimiting("api")]
public sealed class CatalogController(CatalogService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();
}
