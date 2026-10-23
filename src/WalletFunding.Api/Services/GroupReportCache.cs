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

public interface IGroupReportCache
{
    Task<JsonObject?> Get(string key);
    Task<bool> Set(string key, JsonObject value);
}

// Only monthly entries/aggregates are cached. Membership and account state are
// always read from PostgreSQL before this cache is consulted.
public sealed class GroupReportCache(IDistributedCache cache, ILogger<GroupReportCache> logger) : IGroupReportCache
{
    private long retryAfter;
    public async Task<JsonObject?> Get(string key)
    {
        if (Environment.TickCount64 < Interlocked.Read(ref retryAfter)) return null;
        try { var json = await cache.GetStringAsync(key); return json == null ? null : JsonNode.Parse(json)?.AsObject(); }
        catch (Exception e) when (e is RedisException or TimeoutException or System.Text.Json.JsonException)
        { Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + 5000); logger.LogWarning("Group report cache unavailable ({ExceptionType}); reading PostgreSQL", e.GetType().Name); return null; }
    }
    public async Task<bool> Set(string key, JsonObject value)
    {
        if (Environment.TickCount64 < Interlocked.Read(ref retryAfter)) return false;
        try { await cache.SetStringAsync(key, value.ToJsonString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }); return true; }
        catch (Exception e) when (e is RedisException or TimeoutException)
        { Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + 5000); logger.LogWarning("Group report cache write unavailable ({ExceptionType})", e.GetType().Name); return false; }
    }
}
