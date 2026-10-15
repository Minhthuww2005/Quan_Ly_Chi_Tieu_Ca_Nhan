using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace WalletFunding.Api;
[ApiController]
[Route("api/auth")]
[Route("api/security")]
[EnableRateLimiting("api")]
public sealed class AuthController(AuthService service, IWalletRepository repository) : WalletControllerBase(service, repository)
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public Task<IActionResult> Execute() => ExecuteRequest();

    [HttpPost("/api/admin/login")]
    public Task<IActionResult> AdminLogin() => Execute();
}
