using Microsoft.AspNetCore.SignalR;
using RevenueManagement.Api.Infrastructure.Workers;

namespace RevenueManagement.Api.Infrastructure.Realtime;

/// <summary>
/// Thực thi IRevenueChangedPublisher gửi sự kiện Realtime qua SignalR tới các client trong group.
/// </summary>
public sealed class SignalRRevenueChangedPublisher : IRevenueChangedPublisher
{
    private readonly IHubContext<DashboardHub, IRevenueHubClient> _hubContext;

    public SignalRRevenueChangedPublisher(IHubContext<DashboardHub, IRevenueHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishAsync(long groupId, CancellationToken cancellationToken = default)
    {
        var groupName = $"group-{groupId}";
        await _hubContext.Clients.Group(groupName).RevenueChanged(
            groupId,
            "Dữ liệu doanh thu đã được cập nhật",
            DateTime.UtcNow);
    }
}
