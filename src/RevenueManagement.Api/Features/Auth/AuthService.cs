using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Features.Auth;

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtTokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail =
            request.Email.Trim().ToUpperInvariant();

        var user = await _db.Users
            .SingleOrDefaultAsync(
                x =>
                    x.NormalizedEmail == normalizedEmail &&
                    x.IsActive,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (verificationResult ==
            PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verificationResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password);
        }

        var accessToken =
            _tokenService.CreateAccessToken(user);

        var rawRefreshToken =
            _tokenService.CreateRefreshToken();

        var now = _timeProvider.GetUtcNow();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,

            TokenHash =
                _tokenService.HashRefreshToken(
                    rawRefreshToken),

            CreatedAt = now.UtcDateTime,

            ExpiresAt = now
                .AddDays(_jwtOptions.RefreshExpiryDays)
                .UtcDateTime
        };

        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(
            cancellationToken);

        return new TokenResponse(
            accessToken.Token,
            rawRefreshToken,
            accessToken.ExpiresAt);
    }

    public async Task<TokenResponse?> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
            request.RefreshToken))
        {
            return null;
        }

        var tokenHash =
            _tokenService.HashRefreshToken(
                request.RefreshToken);

        var currentToken =
            await _db.RefreshTokens
                .Include(x => x.User)
                .SingleOrDefaultAsync(
                    x => x.TokenHash == tokenHash,
                    cancellationToken);

        if (currentToken is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();

        if (currentToken.RevokedAt is not null ||
            currentToken.ExpiresAt <= now.UtcDateTime ||
            !currentToken.User.IsActive)
        {
            return null;
        }

        var accessToken =
            _tokenService.CreateAccessToken(
                currentToken.User);

        var newRawRefreshToken =
            _tokenService.CreateRefreshToken();

        var replacementToken =
            new RefreshToken
            {
                UserId = currentToken.UserId,

                TokenHash =
                    _tokenService.HashRefreshToken(
                        newRawRefreshToken),

                CreatedAt = now.UtcDateTime,

                ExpiresAt = now
                    .AddDays(
                        _jwtOptions.RefreshExpiryDays)
                    .UtcDateTime
            };

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    cancellationToken);

            _db.RefreshTokens.Add(
                replacementToken);

            await _db.SaveChangesAsync(
                cancellationToken);

            currentToken.RevokedAt =
                now.UtcDateTime;

            currentToken.ReplacedById =
                replacementToken.Id;

            await _db.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new TokenResponse(
                accessToken.Token,
                newRawRefreshToken,
                accessToken.ExpiresAt);
        });
    }
}
