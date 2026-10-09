namespace RevenueManagement.Api.Common.Exceptions;

// TODO(A): base exception và các lỗi Validation/Forbidden/NotFound.
public sealed class AppException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
