namespace RevenueManagement.Api.Common.Middleware;

public sealed class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString("N");

        // Lưu RequestId cho request hiện tại
        context.TraceIdentifier = requestId;

        // Trả RequestId về cho frontend qua response header
        context.Response.Headers["X-Request-ID"] = requestId;

        await _next(context);
    }
}