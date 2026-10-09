namespace RevenueManagement.Api.Infrastructure.Realtime;

/// <summary>
/// Hợp đồng Strongly-Typed Client cho SignalR Hub.
/// Định nghĩa các sự kiện máy chủ gửi về phía frontend client.
/// </summary>
public interface IRevenueHubClient
{
    Task RevenueChanged(long groupId, string message, DateTime timestamp);

    Task ResyncRequired(long groupId, string reason);
}
