using Npgsql;
using Microsoft.AspNetCore.Mvc;
namespace WalletFunding.Api;
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (ApiError e) { await Error(context, e.Status, e.Message, e.Code, e.Errors); }
        catch (System.Text.Json.JsonException) { await Error(context, 400, "JSON không hợp lệ", "INVALID_JSON"); }
        catch (PostgresException e) when (e.SqlState == "23505") { await Error(context, 409, "Dữ liệu đã tồn tại", "DUPLICATE_DATA"); }
        catch (Exception e)
        {
            log.LogError(e, "Wallet request failed: {Path}", context.Request.Path);
            await Error(context, 500, "Lỗi hệ thống", "INTERNAL_ERROR");
        }
    }
    private static async Task Error(HttpContext context, int status, string message, string code, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted) throw new InvalidOperationException("Cannot replace a response that has already started.");
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Type = "about:blank", Status = status,
            Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
            Detail = message, Instance = context.Request.Path
        };
        problem.Extensions["success"] = false;
        problem.Extensions["message"] = message;
        problem.Extensions["code"] = code;
        problem.Extensions["requestId"] = context.TraceIdentifier;
        if (errors is { Count: > 0 }) problem.Extensions["errors"] = errors;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
