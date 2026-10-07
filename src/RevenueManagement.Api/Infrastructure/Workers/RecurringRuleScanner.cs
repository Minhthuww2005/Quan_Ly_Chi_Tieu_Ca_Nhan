using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevenueManagement.Api.Features.Recurring;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Options;

namespace RevenueManagement.Api.Infrastructure.Workers;

/// <summary>
/// 1B.4 — BackgroundService chạy độc lập với request: định kỳ quét quy tắc đến hạn
/// và đẩy RuleId vào <see cref="RevenueJobQueue"/>. Không tự tạo giao dịch; việc đó
/// do <see cref="RecurringRevenueWorker"/> làm, nên quét nhanh và không bị chặn bởi job chậm.
/// </summary>
public sealed class RecurringRuleScanner(
    IServiceScopeFactory scopeFactory,
    RevenueJobQueue queue,
    TimeProvider clock,
    IOptions<WorkerOptions> options,
    ILogger<RecurringRuleScanner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Recurring scanner is disabled (Worker:Enabled = false)");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        logger.LogInformation("Recurring scanner started, interval {Interval}", interval);

        try
        {
            await ScanSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(interval, clock);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ScanSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }

        logger.LogInformation("Recurring scanner stopped");
    }

    /// <summary>Quét một lần; trả về số quy tắc vừa được đưa vào hàng đợi.</summary>
    public async Task<int> ScanOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var utcNow = clock.GetUtcNow();
        // Lọc thô theo UTC + 1 ngày (múi giờ sớm nhất là UTC+14), lọc chính xác theo múi giờ của từng quy tắc bên dưới.
        var horizon = DateOnly.FromDateTime(utcNow.UtcDateTime).AddDays(1);

        var candidates = await db.RecurringRules
            .AsNoTracking()
            .Where(r => r.IsActive && r.NextRunOn != null && r.NextRunOn <= horizon)
            .OrderBy(r => r.NextRunOn)
            .ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.NextRunOn, r.TimeZoneId })
            .Take(options.Value.ScanBatchSize)
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        foreach (var rule in candidates)
        {
            if (rule.NextRunOn > RecurringSchedule.GetLocalDate(rule.TimeZoneId, utcNow)) continue;
            if (await queue.EnqueueAsync(rule.Id, cancellationToken)) enqueued++;
        }

        if (enqueued > 0)
            logger.LogInformation("Recurring scanner enqueued {Count} due rule(s)", enqueued);

        return enqueued;
    }

    private async Task ScanSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ScanOnceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Không để exception làm dừng host; lần quét sau sẽ thử lại.
            logger.LogError(ex, "Recurring scan failed");
        }
    }
}
