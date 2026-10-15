using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WalletFunding.Api;

[ApiController]
[Route("api/payments")]
[EnableRateLimiting("api")]
public sealed class PaymentsController(PaymentService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();
}
