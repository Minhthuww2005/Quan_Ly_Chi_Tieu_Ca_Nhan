using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace WalletFunding.Api;

public static class GroupCacheNamespace
{
    public static string ForDatabase(string connection)
    {
        var db = new NpgsqlConnectionStringBuilder(connection);
        var scope = $"{db.Host}:{db.Port}/{db.Database}";
        return "HTDPay:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)))[..16] + ":";
    }
}

// One Redis connection for the whole app, opened in the background at startup. With abortConnect=false the
// multiplexer keeps reconnecting on its own, so a Redis started after the API is picked up without a restart.
// IsConnected lets the cache skip Redis at once while it is down, instead of waiting for a timeout per request.
public sealed class GroupRedisConnection : IDisposable
{
    private readonly Task<IConnectionMultiplexer> connecting;

    public GroupRedisConnection(string configuration)
    {
        var options = ConfigurationOptions.Parse(configuration);
        options.AbortOnConnectFail = false;
        connecting = Task.Run(async () => (IConnectionMultiplexer)await ConnectionMultiplexer.ConnectAsync(options));
    }

    public bool IsConnected => connecting.IsCompletedSuccessfully && connecting.Result.IsConnected;
    public Task<IConnectionMultiplexer> GetAsync() => connecting;

    public void Dispose()
    {
        if (connecting.IsCompletedSuccessfully) connecting.Result.Dispose();
    }
}

public interface IGroupReportCache
{
    Task<JsonObject?> Get(string key);
    Task<bool> Set(string key, JsonObject value);
}

// Only monthly entries/aggregates are cached. Membership and account state are
// always read from PostgreSQL before this cache is consulted.
public sealed class GroupReportCache(IDistributedCache cache, ILogger<GroupReportCache> logger, GroupRedisConnection? redis = null) : IGroupReportCache
{
    private long retryAfter;
    private int downLogged;

    // Redis not connected (not started, or reconnecting): read PostgreSQL right away (header BYPASS).
    private bool Available()
    {
        if (Environment.TickCount64 < Interlocked.Read(ref retryAfter)) return false;
        if (redis == null || redis.IsConnected) { Interlocked.Exchange(ref downLogged, 0); return true; }
        if (Interlocked.Exchange(ref downLogged, 1) == 0) logger.LogWarning("Group report cache: Redis is not connected; reading PostgreSQL until it is");
        return false;
    }

    public async Task<JsonObject?> Get(string key)
    {
        if (!Available()) return null;
        try { var json = await cache.GetStringAsync(key); return json == null ? null : JsonNode.Parse(json)?.AsObject(); }
        catch (Exception e) when (e is RedisException or TimeoutException or System.Text.Json.JsonException)
        { Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + 5000); logger.LogWarning("Group report cache unavailable ({ExceptionType}); reading PostgreSQL", e.GetType().Name); return null; }
    }
    public async Task<bool> Set(string key, JsonObject value)
    {
        if (!Available()) return false;
        try { await cache.SetStringAsync(key, value.ToJsonString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }); return true; }
        catch (Exception e) when (e is RedisException or TimeoutException)
        { Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + 5000); logger.LogWarning("Group report cache write unavailable ({ExceptionType})", e.GetType().Name); return false; }
    }
}
