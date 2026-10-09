namespace RevenueManagement.Api.Common.Models;

// TODO(A): code, message, requestId.
public sealed class ErrorResponse
{
    public string Code { get; init; } = "request_failed";
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
