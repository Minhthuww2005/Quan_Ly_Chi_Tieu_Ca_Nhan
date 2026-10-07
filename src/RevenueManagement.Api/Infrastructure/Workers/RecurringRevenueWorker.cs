using Microsoft.Extensions.Options;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>
/// 3.2 — Consumer của <see cref="RevenueJobQueue"/>: lấy job, xử lý trong scope DI riêng,
/// lỗi thì retry theo exponential backoff (không chặn các job khác), hết số lần retry thì
/// ghi RunLog Failed. Dừng an toàn khi host shutdown (CancellationToken).
/// </summary>
public sealed class RecurringRevenueWorker(
    IServiceScopeFactory scopeFactory,
    RevenueJobQueue queue,
    TimeProvider clock,
    IOptions<WorkerOptions> options,
    ILogger<RecurringRevenueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Recurring worker is disabled (Worker:Enabled = false)");
            return;
        }

        logger.LogInformation("Recurring worker started");
        try
        {
            await foreach (var job in queue.ReadAllAsync(stoppingToken))
            {
                await HandleAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown: job đang dở chưa commit sẽ được scanner lấy lại ở lần chạy sau.
        }

        logger.LogInformation("Recurring worker stopped");
    }

    public async Task HandleAsync(RecurringRevenueJob job, CancellationToken stoppingToken)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RecurringRuleId"] = job.RuleId,
            ["Attempt"] = job.Attempt
        });

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<RecurringRevenueProcessor>();
            var result = await processor.ProcessRuleAsync(job.RuleId, stoppingToken);

            logger.LogInformation(
                "Recurring rule {RuleId} done on attempt {Attempt}: created {Created}, skipped {Skipped}, already processed {AlreadyProcessed}",
                job.RuleId, job.Attempt, result.Created, result.Skipped, result.AlreadyProcessed);

            queue.MarkCompleted(job.RuleId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            queue.MarkCompleted(job.RuleId);
            throw;
        }
        catch (Exception ex)
        {
            var settings = options.Value;
            if (job.Attempt <= settings.MaxRetryAttempts)
            {
                var delay = RetryBackoff.GetDelay(
                    job.Attempt,
                    TimeSpan.FromSeconds(settings.BaseRetryDelaySeconds),
                    TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds),
                    Random.Shared.NextDouble() * 0.2);

                logger.LogWarning(ex,
                    "Recurring rule {RuleId} failed on attempt {Attempt}; retry {RetryNumber}/{MaxRetries} in {Delay}",
                    job.RuleId, job.Attempt, job.Attempt, settings.MaxRetryAttempts, delay);

                // Không await: consumer tiếp tục xử lý job khác trong lúc chờ backoff.
                _ = RequeueAfterDelayAsync(job.NextAttempt(), delay, stoppingToken);
                return;
            }

            logger.LogError(ex,
                "Recurring rule {RuleId} failed after {Attempts} attempt(s); recording Failed run log",
                job.RuleId, job.Attempt);

            await RecordFailureSafelyAsync(job.RuleId, ex, stoppingToken);
            queue.MarkCompleted(job.RuleId);
        }
    }

    private async Task RequeueAfterDelayAsync(RecurringRevenueJob job, TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, clock, stoppingToken);
            await queue.RequeueAsync(job, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            queue.MarkCompleted(job.RuleId);
        }
        catch (Exception ex)
        {
            queue.MarkCompleted(job.RuleId);
            logger.LogError(ex, "Could not requeue recurring rule {RuleId}", job.RuleId);
        }
    }

    private async Task RecordFailureSafelyAsync(long ruleId, Exception error, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<RecurringRevenueProcessor>();
            var occurrence = await processor.RecordFailureAsync(ruleId, error, stoppingToken);
            if (occurrence is not null)
            {
                logger.LogWarning(
                    "Recurring rule {RuleId} occurrence {OccurrenceDate} marked Failed; schedule advanced",
                    ruleId, occurrence);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ví dụ database vẫn đang lỗi: NextRunOn giữ nguyên nên scanner sẽ thử lại ở chu kỳ sau.
            logger.LogError(ex, "Could not record failure for recurring rule {RuleId}", ruleId);
        }
    }
}
