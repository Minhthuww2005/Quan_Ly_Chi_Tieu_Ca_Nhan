using System.Diagnostics;

namespace RevenueManagement.Api.Features.Reports;

public sealed class ReportRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<ReportRequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/reports"))
        {
            await next(context);
            return;
        }

        var requestId = context.Request.Headers["X-Request-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId))
            requestId = Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;
        context.Response.Headers["X-Request-ID"] = requestId;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId });
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Report request {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
