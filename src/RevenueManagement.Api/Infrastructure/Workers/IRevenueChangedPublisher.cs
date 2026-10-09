namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>
/// Contract RevenueChanged(GroupId), gọi SAU khi transaction database đã commit.
/// B (cache invalidation) và D (SignalR broadcast) đăng ký implementation thật;
/// mặc định là NoOp để module recurring chạy độc lập khi chưa ghép.
/// </summary>
public interface IRevenueChangedPublisher
{
    Task PublishAsync(long groupId, CancellationToken cancellationToken = default);
}
