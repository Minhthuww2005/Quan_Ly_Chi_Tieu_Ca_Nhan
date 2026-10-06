using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RevenueManagement.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthService authService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await authService.LoginAsync(
                request,
                cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email or password."
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await authService.RefreshAsync(
                request,
                cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid or expired refresh token."
            });
        }

        return Ok(result);
    }
}
