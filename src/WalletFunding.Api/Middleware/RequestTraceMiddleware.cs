using System.Diagnostics;

namespace WalletFunding.Api;

public sealed class RequestTraceMiddleware(RequestDelegate next, ILogger<RequestTraceMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
        using var scope = log.BeginScope(new Dictionary<string, object> { ["RequestId"] = context.TraceIdentifier });
        try { await next(context); }
        finally
        {
            log.LogInformation("HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMs:F2} ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
