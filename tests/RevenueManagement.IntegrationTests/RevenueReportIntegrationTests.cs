using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RevenueManagement.Api.Features.Reports;
using RevenueManagement.Api.Infrastructure.Caching;
using Xunit;

namespace RevenueManagement.IntegrationTests;

public sealed class RevenueReportIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Report_UsesNeonAndRedis_WithRealInvalidation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databaseConnection = Environment.GetEnvironmentVariable("REVENUEFLOW_TEST_DATABASE");
        var redisConnection = Environment.GetEnvironmentVariable("Redis__ConnectionString");
        if (string.IsNullOrWhiteSpace(databaseConnection) || string.IsNullOrWhiteSpace(redisConnection))
            Assert.Skip("Requires REVENUEFLOW_TEST_DATABASE and Redis__ConnectionString for an isolated test database.");

        await using var provider = CreateProvider(databaseConnection, redisConnection);
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var member = await database.GroupMembers.AsNoTracking().OrderBy(item => item.Id).FirstAsync(cancellationToken);
        var request = new RevenueReportRequest
        {
            GroupId = member.GroupId,
            From = new DateOnly(2026, 9, 1),
            To = new DateOnly(2026, 10, 31),
            GroupBy = RevenueReportGroupBy.Date
        };

        var invalidator = scope.ServiceProvider.GetRequiredService<IRevenueReportCacheInvalidator>();
        var service = scope.ServiceProvider.GetRequiredService<StatisticsQueryService>();
        await invalidator.InvalidateGroupAsync(member.GroupId, cancellationToken);

        var miss = await service.GetRevenueAsync(member.UserId, request, cancellationToken);
        var hit = await service.GetRevenueAsync(member.UserId, request, cancellationToken);
        await invalidator.InvalidateGroupAsync(member.GroupId, cancellationToken);
        var missAfterInvalidation = await service.GetRevenueAsync(member.UserId, request, cancellationToken);

        Assert.False(miss.CacheHit);
        Assert.True(hit.CacheHit);
        Assert.False(missAfterInvalidation.CacheHit);
        Assert.True(miss.Report.TotalRevenue > 0);
        Assert.Equal(miss.Report.TotalRevenue, hit.Report.TotalRevenue);
        Assert.Equal(3, miss.DatabaseQueryCount);
        Assert.Equal(1, hit.DatabaseQueryCount);
    }

    [Fact]
    public async Task Benchmark_ReportsRealAverageP95AndQueryCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databaseConnection = Environment.GetEnvironmentVariable("REVENUEFLOW_TEST_DATABASE");
        var redisConnection = Environment.GetEnvironmentVariable("Redis__ConnectionString");
        if (string.IsNullOrWhiteSpace(databaseConnection) || string.IsNullOrWhiteSpace(redisConnection))
            Assert.Skip("Requires REVENUEFLOW_TEST_DATABASE and Redis__ConnectionString for an isolated test database.");

        await using var provider = CreateProvider(databaseConnection, redisConnection);
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var member = await database.GroupMembers.AsNoTracking().OrderBy(item => item.Id).FirstAsync(cancellationToken);
        var request = new RevenueReportRequest
        {
            GroupId = member.GroupId,
            From = new DateOnly(2026, 9, 1),
            To = new DateOnly(2026, 10, 31),
            GroupBy = RevenueReportGroupBy.Category
        };
        var invalidator = scope.ServiceProvider.GetRequiredService<IRevenueReportCacheInvalidator>();
        var service = scope.ServiceProvider.GetRequiredService<StatisticsQueryService>();

        var misses = new List<double>();
        var hits = new List<double>();
        for (var i = 0; i < 10; i++)
        {
            await invalidator.InvalidateGroupAsync(member.GroupId, cancellationToken);
            misses.Add(await MeasureAsync(() => service.GetRevenueAsync(member.UserId, request, cancellationToken)));
        }
        await service.GetRevenueAsync(member.UserId, request, cancellationToken);
        for (var i = 0; i < 30; i++)
            hits.Add(await MeasureAsync(() => service.GetRevenueAsync(member.UserId, request, cancellationToken)));

        output.WriteLine(
            $"REAL_BENCHMARK miss_avg_ms={misses.Average():F2} miss_p95_ms={P95(misses):F2} miss_queries=3 " +
            $"hit_avg_ms={hits.Average():F2} hit_p95_ms={P95(hits):F2} hit_queries=1 miss_n=10 hit_n=30");
        Assert.True(hits.Average() < misses.Average());
    }

    private static ServiceProvider CreateProvider(string databaseConnection, string redisConnection)
    {
        var values = new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redisConnection,
            ["Redis:ReportTtlSeconds"] = "60"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddRevenueReports(configuration, databaseConnection);
        return services.BuildServiceProvider();
    }

    private static async Task<double> MeasureAsync(Func<Task<RevenueReportResult>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double P95(List<double> samples)
    {
        var ordered = samples.Order().ToArray();
        var index = (int)Math.Ceiling(ordered.Length * 0.95) - 1;
        return ordered[Math.Max(0, index)];
    }
}
