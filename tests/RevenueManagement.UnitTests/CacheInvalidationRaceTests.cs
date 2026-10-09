using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using RevenueManagement.Api.Features.Reports;
using RevenueManagement.Api.Infrastructure.Caching;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class CacheInvalidationRaceTests
{
    [Fact]
    public async Task QueryStartedBeforeWrite_CannotPopulateNewCacheGeneration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var cache = new RedisCacheService(provider.GetRequiredService<IDistributedCache>(),new(TimeSpan.FromSeconds(60)),NullLogger<RedisCacheService>.Instance);
        var request = new RevenueReportRequest { GroupId = 1, From = new(2026,10,1), To = new(2026,10,10) };
        Assert.Null(await cache.GetAsync(request,ct));
        await cache.InvalidateGroupAsync(1,ct);
        await cache.SetAsync(request,new(1,request.From,request.To,null,RevenueReportGroupBy.Date,100,[],DateTimeOffset.UtcNow),ct);
        Assert.Null(await cache.GetAsync(new RevenueReportRequest { GroupId=1,From=request.From,To=request.To },ct));
    }
}
