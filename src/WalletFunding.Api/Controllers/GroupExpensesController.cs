using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController, Authorize, EnableRateLimiting("api")]
[Route("api/groups")]
[Route("api/admin/groups")]
public sealed class GroupExpensesController(GroupExpenseService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PATCH", "PUT", "DELETE")]
    [Route("{**path}")]
    public async Task<IActionResult> Execute()
    {
        var result = await ExecuteRequest();
        Response.Headers["X-Group-Persistence"] = "EF-Core-PostgreSQL";
        Response.Headers["X-Group-Cache"] = service.ReportCacheStatus;
        return result;
    }
}
