using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController]
[Route("api/transactions")]
[Route("api/transfer")]
[EnableRateLimiting("api")]
public sealed class TransactionsController(TransactionService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();
}
