using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.Api.Options;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RevenueJobQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RevenueJobQueue CreateQueue(int capacity = 10)
        => new(Microsoft.Extensions.Options.Options.Create(new WorkerOptions { QueueCapacity = capacity }));

    [Fact]
    public async Task Same_rule_is_not_enqueued_twice_while_pending()
    {
        var queue = CreateQueue();

        Assert.True(await queue.EnqueueAsync(1, Ct));
        Assert.False(await queue.EnqueueAsync(1, Ct));
        Assert.True(await queue.EnqueueAsync(2, Ct));
        Assert.Equal(2, queue.PendingCount);
    }

    [Fact]
    public async Task Rule_can_be_enqueued_again_after_completion()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(1, Ct);

        await using var reader = queue.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(new RecurringRevenueJob(1), reader.Current);

        queue.MarkCompleted(1);
        Assert.False(queue.IsPending(1));
        Assert.True(await queue.EnqueueAsync(1, Ct));
    }

    [Fact]
    public async Task Requeue_keeps_reservation_and_increments_attempt()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(7, Ct);

        await using var reader = queue.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);
        await reader.MoveNextAsync();
        await queue.RequeueAsync(reader.Current.NextAttempt(), Ct);
        await reader.MoveNextAsync();

        Assert.Equal(2, reader.Current.Attempt);
        Assert.True(queue.IsPending(7));
        Assert.False(await queue.EnqueueAsync(7, Ct));
    }

    [Fact]
    public async Task Full_queue_applies_backpressure_until_cancelled()
    {
        var queue = CreateQueue(capacity: 1);
        await queue.EnqueueAsync(1, Ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.EnqueueAsync(2, cts.Token).AsTask());
        Assert.False(queue.IsPending(2));
    }
}

public sealed class RetryBackoffTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void Delay_doubles_each_retry(int retry, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds),
            RetryBackoff.GetDelay(retry, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60)));

    [Fact]
    public void Delay_is_capped()
        => Assert.Equal(TimeSpan.FromSeconds(60),
            RetryBackoff.GetDelay(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60)));

    [Fact]
    public void Jitter_adds_up_to_fraction()
    {
        var delay = RetryBackoff.GetDelay(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), jitterFraction: 0.2);
        Assert.Equal(TimeSpan.FromSeconds(12), delay);
    }

    [Fact]
    public void Retry_number_must_start_at_one()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            RetryBackoff.GetDelay(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
}
