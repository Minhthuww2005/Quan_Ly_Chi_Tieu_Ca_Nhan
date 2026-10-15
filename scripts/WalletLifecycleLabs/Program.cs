using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevenueManagement.Api.Features.Recurring;
using RevenueManagement.Api.Infrastructure.Workers;
using RevenueManagement.Api.Options;

var results = new List<object>();
void Check(bool condition,string name)
{
    if(!condition)throw new InvalidOperationException("FAIL: "+name);
    results.Add(new {name,status="passed"});Console.WriteLine("PASS: "+name);
}
async Task Until(Func<bool> predicate)
{
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
    while(!predicate())await Task.Delay(20,timeout.Token);
}
Dictionary<string,string?> Config(int capacity)=>new()
{
    ["Jwt:Issuer"]="LifecycleLab",["Jwt:Audience"]="LifecycleLab.Web",
    ["Jwt:SigningKey"]="QA_ONLY_LIFECYCLE_KEY_NOT_FOR_APPLICATION",
    ["Redis:ConnectionString"]="localhost:6379",["Worker:QueueCapacity"]=capacity.ToString()
};

foreach(var environment in new[]{Environments.Development,Environments.Production})
{
    var builder=Host.CreateApplicationBuilder(new HostApplicationBuilderSettings {EnvironmentName=environment});
    builder.Logging.ClearProviders();builder.Configuration.AddInMemoryCollection(Config(0));
    builder.Services.AddAppOptions(builder.Configuration);
    using var host=builder.Build();
    var rejected=false;
    try{await host.StartAsync();}
    catch(OptionsValidationException e){rejected=e.Failures.Any(x=>x.Contains("QueueCapacity"));}
    Check(rejected,$"ValidateOnStart rejects invalid Worker QueueCapacity during {environment} host startup");
}
{
    var builder=Host.CreateApplicationBuilder();builder.Logging.ClearProviders();
    builder.Configuration.AddInMemoryCollection(Config(5));builder.Services.AddAppOptions(builder.Configuration);
    using var host=builder.Build();await host.StartAsync();
    Check(host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value.QueueCapacity==5,"Valid configuration starts a real host and binds queue capacity");
    await host.StopAsync();
}

foreach(var scenario in new[]{"disabled","idle-stop","fault-final","retry-stop"})
{
    var settings=new WorkerOptions {Enabled=scenario!="disabled",QueueCapacity=2,MaxRetryAttempts=scenario=="retry-stop"?3:0,BaseRetryDelaySeconds=2};
    var queue=new RevenueJobQueue(Options.Create(settings));
    var logger=new RecordingLogger();var disposals=new DisposalCounter();
    var builder=Host.CreateApplicationBuilder();builder.Logging.ClearProviders();
    builder.Services.AddSingleton(queue);builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IOptions<WorkerOptions>>(Options.Create(settings));
    builder.Services.AddSingleton<ILogger<RecurringRevenueWorker>>(logger);
    builder.Services.AddSingleton(disposals);builder.Services.AddScoped<ScopeProbe>();
    builder.Services.AddScoped<RecurringRevenueProcessor>(provider=>
    {
        _=provider.GetRequiredService<ScopeProbe>();
        throw new InvalidOperationException("controlled-processor-resolution-fault");
    });
    builder.Services.AddHostedService<RecurringRevenueWorker>();
    using var host=builder.Build();
    await host.StartAsync();
    if(scenario=="disabled")
    {
        await Until(()=>logger.Messages.Any(x=>x.Contains("worker is disabled")));
        Check(logger.Messages.Any(x=>x.Contains("worker is disabled")),"Disabled worker returns without consuming work");
        await host.StopAsync();continue;
    }
    await Until(()=>logger.Messages.Any(x=>x.Contains("worker started")));
    if(scenario=="idle-stop")
    {
        var watch=Stopwatch.StartNew();await host.StopAsync();
        Check(watch.Elapsed<TimeSpan.FromSeconds(5)&&logger.Messages.Any(x=>x.Contains("worker stopped")),"Idle worker exits the actual host when its Channel wait is canceled");
        Check(queue.PendingCount==0,"Idle shutdown leaves no queue reservation");continue;
    }
    await queue.EnqueueAsync(9021);
    if(scenario=="fault-final")
    {
        await Until(()=>queue.PendingCount==0&&logger.Messages.Any(x=>x.Contains("Could not record failure")));
        Check(logger.Messages.Any(x=>x.Contains("controlled-processor-resolution-fault")),"Injected processor resolution fault is recorded by the real worker");
        Check(queue.PendingCount==0,"Exhausted retry path releases the failed rule reservation");
        Check(disposals.Count>=2,"Both failed processing and failure-recording scopes dispose their scoped probes");
        await host.StopAsync();
        Check(logger.Messages.Any(x=>x.Contains("worker stopped")),"Host remains stoppable after a final worker failure");
    }
    else
    {
        await Until(()=>logger.Messages.Any(x=>x.Contains("retry 1/3")));
        Check(queue.IsPending(9021),"Retry backoff keeps the reservation before host cancellation");
        await host.StopAsync();await Until(()=>queue.PendingCount==0);
        Check(queue.PendingCount==0,"Host cancellation interrupts pending retry delay and releases reservation");
        Check(logger.Messages.Any(x=>x.Contains("worker stopped")),"Consumer records stopped after retry-delay cancellation");
    }
}
var output=args.FirstOrDefault()??"docs/report-revision/lifecycle-results.json";
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new {generatedAt=DateTimeOffset.UtcNow,passed=results.Count,scope="real Generic Host with existing independent worker; injected DI fault; no database recovery or HTD PAY integration",results},new JsonSerializerOptions {WriteIndented=true}));
Console.WriteLine($"SUMMARY: {results.Count} lifecycle checks passed.");

sealed class DisposalCounter {public int Count;}
sealed class ScopeProbe(DisposalCounter counter):IDisposable
{
    public void Dispose()=>Interlocked.Increment(ref counter.Count);
}
sealed class RecordingLogger:ILogger<RecurringRevenueWorker>
{
    public ConcurrentQueue<string> Messages {get;}=new();
    public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
    public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        =>Messages.Enqueue(formatter(state,exception)+(exception is null?"":" | "+exception.Message));
}
