using Microsoft.Extensions.DependencyInjection.Extensions;
using RevenueManagement.Api.Features.Revenues;
using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Features.Recurring;

/// <summary>
/// Đăng ký module REV-03. Program.cs chỉ cần gọi builder.Services.AddRecurringRevenue(builder.Configuration).
///
/// Lifetime (1A.2):
/// - RevenueJobQueue: Singleton — một Channel dùng chung giữa API, scanner và worker.
/// - IRevenueChangedPublisher (NoOp): Singleton, không giữ state; B/D có thể đăng ký bản thật.
/// - RevenueValidator, RecurringRuleService, RecurringRevenueProcessor: Scoped — phụ thuộc AppDbContext (scoped).
///   Hosted service là singleton nên phải tạo scope riêng cho mỗi lần quét/mỗi job,
///   không inject DbContext trực tiếp (captive dependency).
/// - RecurringRuleScanner, RecurringRevenueWorker: Hosted service (singleton do host quản lý).
/// </summary>
public static class RecurringServiceCollectionExtensions
{
    public static IServiceCollection AddRecurringRevenue(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.MaxRetryDelaySeconds >= o.BaseRetryDelaySeconds,
                "Worker:MaxRetryDelaySeconds phải >= Worker:BaseRetryDelaySeconds.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRevenueChangedPublisher, NoOpRevenueChangedPublisher>();
        services.AddSingleton<RevenueJobQueue>();

        services.AddScoped<RevenueValidator>();
        services.AddScoped<RecurringRuleService>();
        services.AddScoped<RecurringRevenueProcessor>();

        services.AddHostedService<RecurringRuleScanner>();
        services.AddHostedService<RecurringRevenueWorker>();

        return services;
    }
}
