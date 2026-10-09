using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using RevenueManagement.Api.Infrastructure.Authorization;

namespace RevenueManagement.Api.Infrastructure.Realtime;

/// <summary>
/// SignalR Hub quản lý kết nối và group isolation cho bảng điều khiển doanh thu thời gian thực.
/// </summary>
[Authorize]
public sealed class DashboardHub(IAuthorizationService authorization) : Hub<IRevenueHubClient>
{
    public async Task JoinGroup(long groupId)
    {
        // Trích xuất userId từ claim "sub" (Context.User.FindFirst("sub")?.Value) để sẵn sàng xác thực khi tích hợp Auth
        var allowed = await authorization.AuthorizeAsync(Context.User!, groupId, AuthorizationPolicies.GroupMember);
        if (!allowed.Succeeded) throw new HubException("Bạn không có quyền truy cập nhóm này.");

        var groupName = $"group-{groupId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(long groupId)
    {
        var groupName = $"group-{groupId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
