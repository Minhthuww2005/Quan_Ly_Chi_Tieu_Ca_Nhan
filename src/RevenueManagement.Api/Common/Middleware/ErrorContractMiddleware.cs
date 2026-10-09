using System.Text.Json;
using RevenueManagement.Api.Common.Models;

namespace RevenueManagement.Api.Common.Middleware;

/// <summary>Normalize MVC, binding and authentication errors at the API boundary.</summary>
public sealed class ErrorContractMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            context.Response.Body = original;
            if (context.Response.StatusCode < 400) { buffer.Position = 0; await buffer.CopyToAsync(original); return; }
            var status = context.Response.StatusCode;
            var code = $"http_{status}";
            var message = status switch { 400 => "Dữ liệu yêu cầu không hợp lệ.", 401 => "Cần đăng nhập.",
                403 => "Bạn không có quyền truy cập.", 404 => "Không tìm thấy dữ liệu.", 409 => "Xung đột dữ liệu.", _ => "Không thể xử lý yêu cầu." };
            if (buffer.Length > 0)
            {
                try
                {
                    buffer.Position = 0;
                    using var body = await JsonDocument.ParseAsync(buffer);
                    if (body.RootElement.TryGetProperty("code", out var c)) code = c.GetString() ?? code;
                    if (body.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
                    if (body.RootElement.TryGetProperty("errors", out var errors))
                        message = string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString())));
                }
                catch (JsonException) { }
            }
            context.Response.ContentLength = null;
            await context.Response.WriteAsJsonAsync(new ErrorResponse { Code = code, StatusCode = status,
                Message = message, RequestId = context.TraceIdentifier, Timestamp = DateTime.UtcNow });
        }
        finally { context.Response.Body = original; }
    }
}
