using Microsoft.AspNetCore.SignalR;

namespace RevenueManagement.Api.Infrastructure.Realtime;

/// <summary>
/// SignalR Hub quản lý kết nối và group isolation cho bảng điều khiển doanh thu thời gian thực.
/// </summary>
public sealed class DashboardHub : Hub<IRevenueHubClient>
{
    public async Task JoinGroup(long groupId)
    {
        // Trích xuất userId từ claim "sub" (Context.User.FindFirst("sub")?.Value) để sẵn sàng xác thực khi tích hợp Auth
        var userId = Context.User?.FindFirst("sub")?.Value;

        var groupName = $"group-{groupId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(long groupId)
    {
        var groupName = $"group-{groupId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
