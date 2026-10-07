using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>Một job = xử lý mọi kỳ đến hạn của một quy tắc. Attempt bắt đầu từ 1.</summary>
public sealed record RecurringRevenueJob(long RuleId, int Attempt = 1)
{
    public RecurringRevenueJob NextAttempt() => this with { Attempt = Attempt + 1 };
}

/// <summary>
/// Hàng đợi in-process dùng Channel&lt;T&gt; có giới hạn (bounded, FullMode = Wait → backpressure).
/// Singleton. Giữ tập RuleId đang chờ/đang xử lý để scanner không đẩy trùng một quy tắc
/// nhiều lần; RuleId chỉ được giải phóng khi worker gọi <see cref="MarkCompleted"/>.
/// Idempotency thật sự vẫn nằm ở database (unique RuleId + OccurrenceDate).
/// </summary>
public sealed class RevenueJobQueue
{
    private readonly Channel<RecurringRevenueJob> _channel;
    private readonly ConcurrentDictionary<long, byte> _reserved = new();

    public RevenueJobQueue(IOptions<WorkerOptions> options)
    {
        _channel = Channel.CreateBounded<RecurringRevenueJob>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>Số RuleId đang chờ hoặc đang xử lý.</summary>
    public int PendingCount => _reserved.Count;

    public bool IsPending(long ruleId) => _reserved.ContainsKey(ruleId);

    /// <summary>
    /// Đưa quy tắc vào hàng đợi. Trả về false nếu quy tắc đã có trong hàng đợi/đang chạy.
    /// Chờ khi Channel đầy.
    /// </summary>
    public async ValueTask<bool> EnqueueAsync(long ruleId, CancellationToken cancellationToken = default)
    {
        if (!_reserved.TryAdd(ruleId, 0)) return false;

        try
        {
            await _channel.Writer.WriteAsync(new RecurringRevenueJob(ruleId), cancellationToken);
            return true;
        }
        catch
        {
            _reserved.TryRemove(ruleId, out _);
            throw;
        }
    }

    /// <summary>Đưa lại job để retry; RuleId vẫn được giữ chỗ từ lần đầu.</summary>
    public ValueTask RequeueAsync(RecurringRevenueJob job, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<RecurringRevenueJob> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Giải phóng RuleId sau khi xử lý xong (thành công hoặc thất bại cuối cùng).</summary>
    public void MarkCompleted(long ruleId) => _reserved.TryRemove(ruleId, out _);
}
