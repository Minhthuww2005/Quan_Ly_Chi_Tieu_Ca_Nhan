using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Features.Auth;

public sealed class JwtTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _jwtOptions = jwtOptions.Value;
        _timeProvider = timeProvider;

        _signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
    }

    public GeneratedAccessToken CreateAccessToken(User user)
    {
        var now = _timeProvider.GetUtcNow();

        var expiresAt = now.AddMinutes(
            _jwtOptions.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email),

            new Claim(
                "name",
                user.DisplayName)
        };

        var credentials = new SigningCredentials(
            _signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var rawToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return new GeneratedAccessToken(
            rawToken,
            expiresAt);
    }

    public string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);

        return WebEncoders.Base64UrlEncode(bytes);
    }

    public byte[] HashRefreshToken(string refreshToken)
    {
        return SHA256.HashData(
            Encoding.UTF8.GetBytes(refreshToken));
    }
}

public sealed record GeneratedAccessToken(
    string Token,
    DateTimeOffset ExpiresAt);
