using System.Globalization;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using RevenueManagement.Api.Features.Reports;

namespace RevenueManagement.Api.Infrastructure.Caching;

public interface IRevenueReportCache
{
    Task<RevenueReportResponse?> GetAsync(RevenueReportRequest request, CancellationToken cancellationToken = default);
    Task SetAsync(
        RevenueReportRequest request,
        RevenueReportResponse report,
        CancellationToken cancellationToken = default);
}

public interface IRevenueReportCacheInvalidator
{
    Task InvalidateGroupAsync(long groupId, CancellationToken cancellationToken = default);
}

public sealed record RevenueReportCacheOptions(TimeSpan TimeToLive);

public sealed class RedisCacheService(
    IDistributedCache cache,
    RevenueReportCacheOptions options,
    ILogger<RedisCacheService> logger) : IRevenueReportCache, IRevenueReportCacheInvalidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record ReadKey(string Value);
    private readonly ConditionalWeakTable<RevenueReportRequest, ReadKey> _readKeys = new();

    public async Task<RevenueReportResponse?> GetAsync(
        RevenueReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = await CreateVersionedKeyAsync(request, cancellationToken);
        _readKeys.Remove(request);
        _readKeys.Add(request, new ReadKey(key));
        var json = await cache.GetStringAsync(key, cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<RevenueReportResponse>(json, JsonOptions);
    }

    public async Task SetAsync(
        RevenueReportRequest request,
        RevenueReportResponse report,
        CancellationToken cancellationToken = default)
    {
        // Store under the generation used before querying SQL. If a write invalidates
        // meanwhile, this older snapshot cannot populate the new generation.
        var key = _readKeys.TryGetValue(request, out var readKey) ? readKey.Value
            : await CreateVersionedKeyAsync(request, cancellationToken);
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await cache.SetStringAsync(
            key,
            json,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.TimeToLive },
            cancellationToken);
    }

    public async Task InvalidateGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        var nextVersion = Guid.NewGuid().ToString("N");
        await cache.SetStringAsync(
            VersionKey(groupId),
            nextVersion,
            new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(30) },
            cancellationToken);
        logger.LogInformation("Invalidated revenue report cache for GroupId {GroupId}", groupId);
    }

    public static string CreateKey(RevenueReportRequest request, string version)
    {
        var wallet = request.WalletId?.ToString(CultureInfo.InvariantCulture) ?? "all";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"reports:revenue:g:{request.GroupId}:w:{wallet}:from:{request.From:yyyyMMdd}:to:{request.To:yyyyMMdd}:by:{request.GroupBy.ToString().ToLowerInvariant()}:v:{version}");
    }

    private async Task<string> CreateVersionedKeyAsync(
        RevenueReportRequest request,
        CancellationToken cancellationToken)
    {
        var versionKey = VersionKey(request.GroupId);
        var version = await cache.GetStringAsync(versionKey, cancellationToken);
        if (version is null)
        {
            version = "1";
        }

        return CreateKey(request, version);
    }

    private static string VersionKey(long groupId) => $"reports:revenue:g:{groupId}:version";
}
