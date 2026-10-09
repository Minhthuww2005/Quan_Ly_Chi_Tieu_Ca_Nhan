using System.Security.Claims;

namespace RevenueManagement.Api.Features.Reports;

public interface IReportCurrentUser
{
    bool TryGetUserId(out long userId);
}

internal sealed class HttpReportCurrentUser(IHttpContextAccessor httpContextAccessor) : IReportCurrentUser
{
    public bool TryGetUserId(out long userId)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal?.FindFirstValue("sub")
                    ?? principal?.FindFirstValue("userId");
        return long.TryParse(value, out userId) && userId > 0;
    }
}
