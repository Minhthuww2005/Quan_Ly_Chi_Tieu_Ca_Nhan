using RevenueManagement.Api.Infrastructure.Workers;

namespace RevenueManagement.Api.Infrastructure.Realtime;

public static class RealtimeDependencyInjection
{
    /// <summary>
    /// Đăng ký dịch vụ SignalR và triển khai SignalR cho IRevenueChangedPublisher.
    /// </summary>
    public static IServiceCollection AddRealtimeServices(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRevenueChangedPublisher, SignalRRevenueChangedPublisher>();

        return services;
    }
}
