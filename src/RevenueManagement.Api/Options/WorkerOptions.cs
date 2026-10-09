using System.ComponentModel.DataAnnotations;

namespace RevenueManagement.Api.Options;

/// <summary>
/// Cấu hình section "Worker" cho recurring scanner/queue/worker.
/// Bind + ValidateDataAnnotations + ValidateOnStart (fail-fast) trong AddAppOptions.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Tắt scanner/worker (ví dụ trong integration test không có database).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét quy tắc đến hạn.</summary>
    [Range(1, 3600, ErrorMessage = "Worker:PollIntervalSeconds phải từ 1 đến 3600 giây.")]
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>Số lần retry sau lần chạy đầu tiên. Hết retry thì ghi RunLog Failed.</summary>
    [Range(0, 10, ErrorMessage = "Worker:MaxRetryAttempts phải từ 0 đến 10 lần.")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Độ trễ retry đầu tiên; các lần sau nhân đôi (exponential backoff).</summary>
    [Range(1, 600, ErrorMessage = "Worker:BaseRetryDelaySeconds phải từ 1 đến 600 giây.")]
    public int BaseRetryDelaySeconds { get; set; } = 2;

    /// <summary>Trần độ trễ giữa hai lần retry.</summary>
    [Range(1, 3600, ErrorMessage = "Worker:MaxRetryDelaySeconds phải từ 1 đến 3600 giây.")]
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>Sức chứa Channel; đầy thì scanner chờ (backpressure) thay vì làm mất job.</summary>
    [Range(1, 100_000, ErrorMessage = "Worker:QueueCapacity phải từ 1 đến 100,000.")]
    public int QueueCapacity { get; set; } = 500;

    /// <summary>Số quy tắc tối đa lấy trong một lần quét.</summary>
    [Range(1, 10_000, ErrorMessage = "Worker:ScanBatchSize phải từ 1 đến 10,000.")]
    public int ScanBatchSize { get; set; } = 200;

    /// <summary>Số kỳ tối đa bù cho một quy tắc trong một lần xử lý (khi server tắt nhiều ngày).</summary>
    [Range(1, 366, ErrorMessage = "Worker:MaxCatchUpOccurrences phải từ 1 đến 366.")]
    public int MaxCatchUpOccurrences { get; set; } = 31;
}
