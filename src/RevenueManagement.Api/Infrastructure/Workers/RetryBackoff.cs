namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>Exponential backoff: base * 2^(retry-1), có trần và jitter để tránh retry dồn cùng lúc.</summary>
public static class RetryBackoff
{
    /// <param name="retryNumber">Lần retry thứ mấy, bắt đầu từ 1.</param>
    /// <param name="jitterFraction">0..1, phần ngẫu nhiên cộng thêm (0 = không jitter, dùng trong test).</param>
    public static TimeSpan GetDelay(int retryNumber, TimeSpan baseDelay, TimeSpan maxDelay, double jitterFraction = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retryNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(jitterFraction, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitterFraction, 1);

        var exponent = Math.Min(retryNumber - 1, 30);
        var ticks = baseDelay.Ticks * Math.Pow(2, exponent) * (1 + jitterFraction);
        return TimeSpan.FromTicks((long)Math.Round(Math.Min(ticks, maxDelay.Ticks)));
    }
}
