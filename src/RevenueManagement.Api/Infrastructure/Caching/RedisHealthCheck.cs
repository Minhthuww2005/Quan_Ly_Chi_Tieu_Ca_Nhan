using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RevenueManagement.Api.Infrastructure.Caching;

public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"health:{Guid.NewGuid():N}";
            await cache.SetStringAsync(
                key,
                "ok",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) },
                cancellationToken);
            var value = await cache.GetStringAsync(key, cancellationToken);
            await cache.RemoveAsync(key, cancellationToken);
            return value == "ok"
                ? HealthCheckResult.Healthy("Redis read/write succeeded.")
                : HealthCheckResult.Unhealthy("Redis returned an unexpected value.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
