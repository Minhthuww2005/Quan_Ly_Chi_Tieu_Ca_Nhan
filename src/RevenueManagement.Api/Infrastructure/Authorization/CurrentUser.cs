namespace RevenueManagement.Api.Infrastructure.Authorization;

public sealed class CurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor
            .HttpContext?
            .User
            .Identity?
            .IsAuthenticated == true;

    public long? UserId
    {
        get
        {
            var value = _httpContextAccessor
                .HttpContext?
                .User
                .FindFirst("sub")?
                .Value;

            return long.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }
}
