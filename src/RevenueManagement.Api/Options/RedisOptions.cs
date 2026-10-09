using System.ComponentModel.DataAnnotations;

namespace RevenueManagement.Api.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required(ErrorMessage = "Redis:ConnectionString không được để trống.")]
    public string ConnectionString { get; set; } = "localhost:6379";

    [Range(5, 3600, ErrorMessage = "Redis:ReportTtlSeconds phải từ 5 đến 3600 giây.")]
    public int ReportTtlSeconds { get; set; } = 60;

    public string InstanceName { get; set; } = "RevenueManagement:";
}
