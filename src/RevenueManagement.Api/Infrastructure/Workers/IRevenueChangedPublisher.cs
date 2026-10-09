namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>
/// Contract RevenueChanged(GroupId), gọi SAU khi transaction database đã commit.
/// B (cache invalidation) và D (SignalR broadcast) đăng ký implementation thật;
/// mặc định là NoOp để module recurring chạy độc lập khi chưa ghép.
/// Đăng ký bằng services.AddSingleton&lt;IRevenueChangedPublisher, ...&gt;() sẽ ghi đè NoOp
/// (NoOp dùng TryAdd).
/// </summary>
public interface IRevenueChangedPublisher
{
    Task PublishAsync(long groupId, CancellationToken cancellationToken = default);
}

public sealed class NoOpRevenueChangedPublisher(ILogger<NoOpRevenueChangedPublisher> logger)
    : IRevenueChangedPublisher
{
    public Task PublishAsync(long groupId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("RevenueChanged for group {GroupId} (no subscriber registered)", groupId);
        return Task.CompletedTask;
    }
}
