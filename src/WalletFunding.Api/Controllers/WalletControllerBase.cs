using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace WalletFunding.Api;

public abstract class WalletControllerBase(IWalletService service, IWalletRepository repository) : ControllerBase
{
    protected async Task<IActionResult> ExecuteRequest()
    {
        var body = new JsonObject();
        if (Request.Method != "GET" && HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true)
            body = (await JsonNode.ParseAsync(Request.Body))?.AsObject() ?? new JsonObject();

        var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        headers["X-HTD-Client-IP"] = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var request = new WalletRequest(Request.Method, Request.Path.Value ?? "", body,
            new RequestValues(Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString())),
            new RequestValues(headers),
            User);

        // Match non-empty segments conservatively with MVC's case-insensitive routing.
        // Trailing/repeated separators must not turn a settling IPN into a read unit of work.
        var segments = (Request.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isSettlingIpn = segments.Length >= 4
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("payments", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("vnpay", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("ipn", StringComparison.OrdinalIgnoreCase);
        await service.PrefetchAsync(request);
        await repository.Begin(Request.Method != "GET" || isSettlingIpn);
        var result = await service.ExecuteAsync(request);
        await repository.Commit();
        return new JsonResult(result.Data) { StatusCode = result.StatusCode };
    }
}
