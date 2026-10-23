using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace WalletFunding.Api;

public sealed class GroupEventQueue
{
    // The channel is a bounded wake-up queue, not the durable event store.
    private readonly Channel<int> channel = Channel.CreateBounded<int>(new BoundedChannelOptions(128)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public void Signal(int groupId) => channel.Writer.TryWrite(groupId);
    public ChannelReader<int> Reader => channel.Reader;
}

[Authorize]
public sealed class GroupExpensesHub(IWalletRepository repository) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await repository.Begin(false);
        var uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = (await repository.All("users")).FirstOrDefault(u => J.Str(u, "user_id") == uid);
        if (user == null || new[] { "blocked", "closed", "frozen", "suspicious", "inactive", "suspended" }.Contains(J.Str(user, "accountStatus", J.Str(user, "status", "active")).ToLowerInvariant()))
        { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }
    // No client-controlled JoinGroup: publication resolves current membership
    // from PostgreSQL and targets authenticated user identifiers.
}

public sealed class GroupEventWorker(GroupEventQueue queue, NpgsqlDataSource source,
    IHubContext<GroupExpensesHub> hub, ILogger<GroupEventWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanner = Scan(stoppingToken);
        try
        {
            await foreach (var hint in queue.Reader.ReadAllAsync(stoppingToken))
            {
                while (queue.Reader.TryRead(out _)) { }
                try { while (await ProcessBatch(stoppingToken) == 20) { } }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { logger.LogWarning("Group outbox scan failed ({ExceptionType}); will retry", e.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { try { await scanner; } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { } }
    }
    private async Task Scan(CancellationToken token)
    {
        queue.Signal(0);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(token)) queue.Signal(0);
    }
    private async Task<int> ProcessBatch(CancellationToken token)
    {
        await using var c = await source.OpenConnectionAsync(token);
        await using var tx = await c.BeginTransactionAsync(token);
        var pending = new List<(Guid Id, int GroupId, int Attempts)>();
        await using (var cmd = new NpgsqlCommand("SELECT event_id,group_id,attempts FROM htdpay.group_outbox WHERE processed_at IS NULL AND next_attempt_at<=now() ORDER BY created_at LIMIT 20 FOR UPDATE SKIP LOCKED", c, tx))
        await using (var reader = await cmd.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) pending.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2)));
        foreach (var item in pending)
        {
            try
            {
                // Recheck active accounts/membership at delivery; never publish
                // amounts, titles or member names in an invalidation message.
                var recipients = new List<string>();
                await using (var cmd = new NpgsqlCommand("""
                    SELECT DISTINCT u.data->>'user_id' FROM htdpay.users u
                    WHERE lower(COALESCE(u.data->>'accountStatus',u.data->>'status','active')) NOT IN ('blocked','closed','frozen','suspicious','inactive','suspended')
                    AND (u.data->>'role'='admin' OR EXISTS (
                        SELECT 1 FROM htdpay.expensemembers m WHERE m.data->>'group_id'=@group
                        AND m.data->>'user_id'=u.data->>'user_id' AND m.data->>'active'='true'))
                    """, c, tx))
                {
                    cmd.Parameters.AddWithValue("group", item.GroupId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    await using var reader = await cmd.ExecuteReaderAsync(token);
                    while (await reader.ReadAsync(token)) recipients.Add(reader.GetString(0));
                }
                await hub.Clients.Users(recipients).SendAsync("GroupChanged", new { eventId = item.Id, groupId = item.GroupId }, token);
                await using var done = new NpgsqlCommand("UPDATE htdpay.group_outbox SET processed_at=now(),attempts=attempts+1 WHERE event_id=@id", c, tx);
                done.Parameters.AddWithValue("id", item.Id); await done.ExecuteNonQueryAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is not NpgsqlException)
            {
                var seconds = Math.Min(60, Math.Pow(2, Math.Min(item.Attempts + 1, 6)));
                await using var retry = new NpgsqlCommand("UPDATE htdpay.group_outbox SET attempts=attempts+1,next_attempt_at=now()+@delay WHERE event_id=@id", c, tx);
                retry.Parameters.AddWithValue("id", item.Id); retry.Parameters.AddWithValue("delay", TimeSpan.FromSeconds(seconds)); await retry.ExecuteNonQueryAsync(token);
                logger.LogWarning("Group event {EventId} delivery failed ({ExceptionType}); retry scheduled", item.Id, e.GetType().Name);
            }
        }
        await tx.CommitAsync(token);
        return pending.Count;
    }
}
