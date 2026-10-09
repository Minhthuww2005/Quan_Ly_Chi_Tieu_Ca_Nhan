using RevenueManagement.Api.Infrastructure.Caching;
using RevenueManagement.Api.Infrastructure.Workers;

namespace RevenueManagement.Api.Infrastructure.Realtime;

/// <summary>One post-commit integration point shared by manual and recurring writes.</summary>
public sealed class RevenueChangedPublisher(IRevenueReportCacheInvalidator cache,
    SignalRRevenueChangedPublisher realtime, ILogger<RevenueChangedPublisher> logger) : IRevenueChangedPublisher
{
    public async Task PublishAsync(long groupId, CancellationToken cancellationToken = default)
    {
        // Each subscriber is independent. A committed write must not become an HTTP 500
        // that invites a client to repeat the write merely because Redis is unavailable.
        try { await cache.InvalidateGroupAsync(groupId, cancellationToken); }
        catch (Exception ex) { logger.LogError(ex, "Post-commit cache invalidation failed for {GroupId}", groupId); }
        try { await realtime.PublishAsync(groupId, cancellationToken); }
        catch (Exception ex) { logger.LogError(ex, "Post-commit realtime notification failed for {GroupId}", groupId); }
    }
}
