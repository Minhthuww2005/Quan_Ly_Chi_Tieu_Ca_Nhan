using System.ComponentModel.DataAnnotations;
namespace RevenueManagement.Api.Features.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(200, MinimumLength = 8)] string Password);

public sealed record RefreshTokenRequest(
    [Required] string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);
