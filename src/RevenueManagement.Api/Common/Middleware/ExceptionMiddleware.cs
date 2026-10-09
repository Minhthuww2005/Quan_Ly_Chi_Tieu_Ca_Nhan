using System.Text.Json;
using RevenueManagement.Api.Common.Models;
using RevenueManagement.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace RevenueManagement.Api.Common.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var requestId = context.TraceIdentifier;

            // Ghi lỗi thật vào log phía server
            _logger.LogError(
                ex,
                "Unhandled exception. RequestId: {RequestId}",
                requestId);

            // Nếu response đã được gửi thì không thể ghi lại response mới
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            var status = ex switch { AppException e => e.StatusCode, DbUpdateConcurrencyException => 409,
                BadHttpRequestException => 400, _ => 500 };
            context.Response.StatusCode = status;

            context.Response.ContentType =
                "application/json; charset=utf-8";

            context.Response.Headers["X-Request-ID"] =
                requestId;

            var errorResponse = new ErrorResponse
            {
                StatusCode = status,
                Code = ex is AppException appError ? appError.Code : status == 409 ? "concurrency_conflict" : status == 400 ? "invalid_request" : "internal_error",
                Message = ex is AppException ? ex.Message : status == 409 ? "Dữ liệu đã thay đổi. Hãy tải lại." : status == 400 ? "Dữ liệu yêu cầu không hợp lệ." : "An unexpected error occurred.",
                RequestId = requestId,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(
                errorResponse,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

            await context.Response.WriteAsync(json);
        }
    }
}
