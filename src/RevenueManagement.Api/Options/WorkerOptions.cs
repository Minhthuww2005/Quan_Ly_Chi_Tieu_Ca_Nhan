using System.ComponentModel.DataAnnotations;

namespace RevenueManagement.Api.Options;

/// <summary>
/// Cấu hình section "Worker" cho recurring scanner/queue/worker.
/// Bind + ValidateDataAnnotations + ValidateOnStart (fail-fast) trong AddRecurringRevenue.
/// Đặt lại qua environment, ví dụ Worker__PollIntervalSeconds=10.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Tắt scanner/worker (ví dụ trong integration test không có database).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét quy tắc đến hạn.</summary>
    [Range(1, 3600)]
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>Số lần retry sau lần chạy đầu tiên. Hết retry thì ghi RunLog Failed.</summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Độ trễ retry đầu tiên; các lần sau nhân đôi (exponential backoff).</summary>
    [Range(1, 600)]
    public int BaseRetryDelaySeconds { get; set; } = 2;

    /// <summary>Trần độ trễ giữa hai lần retry.</summary>
    [Range(1, 3600)]
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>Sức chứa Channel; đầy thì scanner chờ (backpressure) thay vì làm mất job.</summary>
    [Range(1, 100_000)]
    public int QueueCapacity { get; set; } = 500;

    /// <summary>Số quy tắc tối đa lấy trong một lần quét.</summary>
    [Range(1, 10_000)]
    public int ScanBatchSize { get; set; } = 200;

    /// <summary>Số kỳ tối đa bù cho một quy tắc trong một lần xử lý (khi server tắt nhiều ngày).</summary>
    [Range(1, 366)]
    public int MaxCatchUpOccurrences { get; set; } = 31;
}
