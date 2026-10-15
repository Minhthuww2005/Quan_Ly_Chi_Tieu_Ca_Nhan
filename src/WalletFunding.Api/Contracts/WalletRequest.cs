using System.Security.Claims;
using System.Text.Json.Nodes;

namespace WalletFunding.Api;

// Transport-neutral request: services do not depend on HttpContext or MVC.
public sealed record WalletRequest(
    string Method,
    string Path,
    JsonObject Body,
    RequestValues Query,
    RequestValues Headers,
    ClaimsPrincipal Principal);

public sealed class RequestValues(IReadOnlyDictionary<string, string> values)
{
    public IReadOnlyDictionary<string, string> Values => values;
    public string this[string key] => values.GetValueOrDefault(key, "");
}

public sealed record ServiceResponse(JsonObject Data, int StatusCode = 200);
