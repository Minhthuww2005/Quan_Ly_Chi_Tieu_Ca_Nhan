using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace WalletFunding.Api;

public sealed record GoogleIdentity(string Subject, string Email, string Name, string? Picture);

public interface IGoogleIdentityVerifier
{
    Task<GoogleIdentity> VerifyAsync(string credential);
}

public sealed class GoogleIdentityVerifier(IConfiguration config, IHttpClientFactory clients) : IGoogleIdentityVerifier
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> metadata = new(
        "https://accounts.google.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever(clients.CreateClient("GoogleMetadata")) { RequireHttps = true });

    public async Task<GoogleIdentity> VerifyAsync(string credential)
    {
        var clientId = config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) throw new ApiError(503, "Chưa cấu hình Google OAuth Client ID");
        if (credential.Length > 16_384 || !new JwtSecurityTokenHandler().CanReadToken(credential))
            throw new ApiError(401, "Google credential không hợp lệ");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var keys = await metadata.GetConfigurationAsync(timeout.Token);
            return Validate(credential, clientId, keys.SigningKeys);
        }
        catch (ApiError) { throw; }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or InvalidOperationException)
        { throw new ApiError(503, "Không kết nối được dịch vụ xác minh Google; vui lòng thử lại"); }
    }

    public static GoogleIdentity Validate(string credential, string clientId, IEnumerable<SecurityKey> signingKeys)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(credential,
                new TokenValidationParameters
                {
                    ValidIssuers = ["https://accounts.google.com", "accounts.google.com"], ValidAudience = clientId,
                    IssuerSigningKeys = signingKeys, ValidateLifetime = true, ValidateIssuer = true,
                    ValidateAudience = true, ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30)
                }, out _);
            var email = principal.FindFirst("email")?.Value ?? "";
            var subject = principal.FindFirst("sub")?.Value ?? "";
            if (!bool.TryParse(principal.FindFirst("email_verified")?.Value, out var verified) || !verified || email.Length == 0 || subject.Length == 0)
                throw new ApiError(401, "Google email chưa được xác minh");
            return new GoogleIdentity(subject, email.Trim().ToLowerInvariant(), principal.FindFirst("name")?.Value ?? email, principal.FindFirst("picture")?.Value);
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        { throw new ApiError(401, "Google credential không hợp lệ hoặc đã hết hạn"); }
    }
}
