using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Infrastructure.Caching;

namespace RevenueManagement.Api.Features.Reports;

public static class ReportsDependencyInjection
{
    public static IServiceCollection AddRevenueReports(
        this IServiceCollection services,
        IConfiguration configuration,
        string databaseConnection)
    {
        var redisConnection = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnection))
            throw new InvalidOperationException("Thiếu Redis:ConnectionString cho cache báo cáo.");

        var ttlSeconds = configuration.GetValue<int?>("Redis:ReportTtlSeconds") ?? 60;
        if (ttlSeconds is < 5 or > 3600)
            throw new InvalidOperationException("Redis:ReportTtlSeconds phải từ 5 đến 3600 giây.");

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(databaseConnection, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "RevenueManagement:";
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new RevenueReportCacheOptions(TimeSpan.FromSeconds(ttlSeconds)));
        services.AddSingleton<IRevenueReportCache, RedisCacheService>();
        services.AddSingleton<IRevenueReportCacheInvalidator>(provider =>
            (RedisCacheService)provider.GetRequiredService<IRevenueReportCache>());
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");
        services.AddScoped<IReportCurrentUser, HttpReportCurrentUser>();
        services.AddScoped<StatisticsQueryService>();
        return services;
    }
}
