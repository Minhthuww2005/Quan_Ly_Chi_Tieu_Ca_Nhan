namespace RevenueManagement.Api.Options;

public static class OptionsDependencyInjection
{
    /// <summary>
    /// Đăng ký và kiểm tra toàn bộ Options của ứng dụng theo Options Pattern chuẩn ASP.NET Core:
    /// - Bind theo section tương ứng trong cấu hình
    /// - Kiểm tra tính hợp lệ bằng Data Annotations
    /// - Kích hoạt ValidateOnStart() để phát hiện lỗi ngay lúc khởi động (fail-fast)
    /// </summary>
    public static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
