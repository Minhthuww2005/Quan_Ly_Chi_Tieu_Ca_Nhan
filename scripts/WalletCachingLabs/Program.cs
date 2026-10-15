using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RevenueManagement.Api.Features.Reports;
using RevenueManagement.Api.Infrastructure.Caching;

var prefix="HTDPay.ReportLabs."+Guid.NewGuid().ToString("N")+":";
ServiceProvider Provider()=>new ServiceCollection().AddStackExchangeRedisCache(options=>
{
    options.Configuration="localhost:6379,abortConnect=false,connectTimeout=3000";
    options.InstanceName=prefix;
}).BuildServiceProvider();
using var first=Provider();using var second=Provider();
var backing=first.GetRequiredService<IDistributedCache>();
RedisCacheService Cache(IDistributedCache store,TimeSpan? ttl=null)=>new(store,new(ttl??TimeSpan.FromSeconds(5)),NullLogger<RedisCacheService>.Instance);
var cache=Cache(backing);var other=Cache(second.GetRequiredService<IDistributedCache>());
RevenueReportRequest Request(long group=78001,long? wallet=null,int day=1,RevenueReportGroupBy grouping=RevenueReportGroupBy.Date)=>new()
{GroupId=group,WalletId=wallet,From=new DateOnly(2026,10,day),To=new DateOnly(2026,10,14),GroupBy=grouping};
RevenueReportResponse Snapshot(RevenueReportRequest request,decimal value)=>new(request.GroupId,request.From,request.To,request.WalletId,request.GroupBy,value,[new("lab","Synthetic fixture",value)],DateTimeOffset.UtcNow);
var results=new List<object>();
void Check(bool condition,string name)
{if(!condition)throw new InvalidOperationException("FAIL: "+name);results.Add(new{name,status="passed"});Console.WriteLine("PASS: "+name);}

var request=Request();
Check(await cache.GetAsync(request) is null,"Cold request misses in the isolated Redis namespace");
await cache.SetAsync(request,Snapshot(request,123));
var hit=await cache.GetAsync(Request());
Check(hit?.TotalRevenue==123&&hit.Breakdown.Count==1,"Redis round trip preserves typed snapshot and breakdown");
Check((await other.GetAsync(Request()))?.TotalRevenue==123,"A second cache client observes the same Redis snapshot");
Check(await cache.GetAsync(Request(group:78002)) is null,"Group dimension separates cache entries");
Check(await cache.GetAsync(Request(wallet:99)) is null,"Wallet dimension separates cache entries");
Check(await cache.GetAsync(Request(day:2)) is null,"Date range dimension separates cache entries");
Check(await cache.GetAsync(Request(grouping:RevenueReportGroupBy.Category)) is null,"Grouping dimension separates cache entries");
await cache.InvalidateGroupAsync(78001);
Check(await other.GetAsync(Request()) is null,"A second cache client observes generation invalidation");

var stale=Request(group:78003);
Check(await cache.GetAsync(stale) is null,"Stale query captures its original generation on cache miss");
await other.InvalidateGroupAsync(78003);
await cache.SetAsync(stale,Snapshot(stale,100));
Check(await other.GetAsync(Request(group:78003)) is null,"Old query result cannot populate the new generation in real Redis");
var fresh=Request(group:78003);await other.GetAsync(fresh);await other.SetAsync(fresh,Snapshot(fresh,200));
Check((await cache.GetAsync(Request(group:78003)))?.TotalRevenue==200,"Fresh generation can be populated and read across cache clients");

var shortCache=Cache(backing,TimeSpan.FromSeconds(2));
var expiry=Request(group:78004);await shortCache.GetAsync(expiry);await shortCache.SetAsync(expiry,Snapshot(expiry,9));
Check((await shortCache.GetAsync(Request(group:78004)))?.TotalRevenue==9,"Short TTL fixture is readable before expiration");
var watch=Stopwatch.StartNew();var expired=false;
while(watch.Elapsed<TimeSpan.FromSeconds(8))
{
    if(await shortCache.GetAsync(Request(group:78004)) is null){expired=true;break;}
    await Task.Delay(20);
}
Check(expired,"Redis TTL expires the synthetic snapshot within the bounded wait");
foreach(var group in new long[]{78001,78002,78003,78004})await backing.RemoveAsync($"reports:revenue:g:{group}:version");
var result=new{generatedAt=DateTimeOffset.UtcNow,passed=results.Count,scope="existing cache service against actual local Redis with isolated random InstanceName; no SQL query, HTD PAY integration, load ranking or cross-process test",namespacePrefix=prefix,expiryWaitMs=watch.Elapsed.TotalMilliseconds,results};
await File.WriteAllTextAsync(args.FirstOrDefault()??"docs/report-revision/caching-results.json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"SUMMARY: {results.Count} actual Redis cache checks passed.");
