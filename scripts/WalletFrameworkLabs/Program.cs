using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WalletFunding.Api;

var results = new List<object>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    results.Add(new { name, status = "passed" });
    Console.WriteLine("PASS: " + name);
}
DefaultHttpContext Context()
{
    var context = new DefaultHttpContext();
    context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
    context.Request.Path = "/api/lab";
    context.TraceIdentifier = "wallet-lab-request";
    context.Response.Body = new MemoryStream();
    return context;
}
async Task<JsonElement> Body(DefaultHttpContext context)
{
    context.Response.Body.Position = 0;
    using var document = await JsonDocument.ParseAsync(context.Response.Body);
    return document.RootElement.Clone();
}

var ordered = Context();
var handler = new ApiExceptionMiddleware(_ => throw new InvalidOperationException("controlled-private-detail"), NullLogger<ApiExceptionMiddleware>.Instance);
await new RequestTraceMiddleware(handler.InvokeAsync, NullLogger<RequestTraceMiddleware>.Instance).InvokeAsync(ordered);
var error = await Body(ordered);
Check(ordered.Response.StatusCode == 500, "Exception handler before failing delegate maps error to HTTP 500");
Check(ordered.Response.ContentType!.StartsWith("application/problem+json"), "Error uses ProblemDetails content type");
Check(error.GetProperty("status").GetInt32() == 500 && error.GetProperty("type").GetString() == "about:blank", "Standard ProblemDetails fields match HTTP status");
Check(error.GetProperty("requestId").GetString() == ordered.Response.Headers["X-Request-Id"], "Header and error body share request ID");
Check(!error.ToString().Contains("controlled-private-detail"), "Unexpected exception detail is not exposed to client");

var invalidPin = Context();
await new ApiExceptionMiddleware(_ => throw new ApiError(401, "PIN không hợp lệ", "INVALID_PIN"), NullLogger<ApiExceptionMiddleware>.Instance).InvokeAsync(invalidPin);
error = await Body(invalidPin);
Check(invalidPin.Response.StatusCode == 401 && error.GetProperty("code").GetString() == "INVALID_PIN" && !error.GetProperty("success").GetBoolean(), "Legacy PIN contract remains compatible inside ProblemDetails");

var json = Context();
await new ApiExceptionMiddleware(_ => throw new JsonException("bad input"), NullLogger<ApiExceptionMiddleware>.Instance).InvokeAsync(json);
Check(json.Response.StatusCode == 400 && (await Body(json)).GetProperty("code").GetString() == "INVALID_JSON", "Malformed JSON has stable HTTP 400 code");

var handlerEntered = false;
RequestDelegate wrongOrder = async context =>
{
    if (!handlerEntered) throw new InvalidOperationException("before-handler");
    await new ApiExceptionMiddleware(_ => { handlerEntered = true; return Task.CompletedTask; }, NullLogger<ApiExceptionMiddleware>.Instance).InvokeAsync(context);
};
try { await wrongOrder(Context()); throw new Exception("Wrong order did not fail"); }
catch (InvalidOperationException e) when (e.Message == "before-handler") { Check(!handlerEntered, "Exception raised before handler escapes handler when middleware order is reversed"); }

var sequence = new List<string>();
RequestDelegate endpoint = _ => { sequence.Add("endpoint"); return Task.CompletedTask; };
RequestDelegate inner = async context => { sequence.Add("inner-in"); await endpoint(context); sequence.Add("inner-out"); };
RequestDelegate outer = async context => { sequence.Add("outer-in"); await inner(context); sequence.Add("outer-out"); };
await outer(Context());
Check(sequence.SequenceEqual(new[] { "outer-in", "inner-in", "endpoint", "inner-out", "outer-out" }), "Response unwinds middleware in reverse order");

var services = new ServiceCollection();
services.AddTransient<TransientProbe>(); services.AddScoped<ScopedProbe>(); services.AddSingleton<SingletonProbe>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
using var scope1 = provider.CreateScope(); using var scope2 = provider.CreateScope();
Check(!ReferenceEquals(scope1.ServiceProvider.GetRequiredService<TransientProbe>(), scope1.ServiceProvider.GetRequiredService<TransientProbe>()), "Transient resolves a new instance per resolution");
Check(ReferenceEquals(scope1.ServiceProvider.GetRequiredService<ScopedProbe>(), scope1.ServiceProvider.GetRequiredService<ScopedProbe>()) && !ReferenceEquals(scope1.ServiceProvider.GetRequiredService<ScopedProbe>(), scope2.ServiceProvider.GetRequiredService<ScopedProbe>()), "Scoped reuses within one scope and separates two scopes");
Check(ReferenceEquals(scope1.ServiceProvider.GetRequiredService<SingletonProbe>(), scope2.ServiceProvider.GetRequiredService<SingletonProbe>()), "Singleton is shared across scopes");
services.AddSingleton<CaptiveProbe>();
try { using var invalid = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }); throw new Exception("Captive dependency was accepted"); }
catch (AggregateException) { Check(true, "ValidateOnBuild rejects a singleton capturing a scoped service"); }

var canceled = Context(); canceled.RequestAborted = new CancellationToken(true);
try { await new ApiExceptionMiddleware(_ => throw new OperationCanceledException(canceled.RequestAborted), NullLogger<ApiExceptionMiddleware>.Instance).InvokeAsync(canceled); throw new Exception("Cancellation was swallowed"); }
catch (OperationCanceledException) { Check(canceled.Response.StatusCode != 500, "Client cancellation is propagated instead of reported as server failure"); }

try { await HttpEndpointLabs.Run(Check); }
catch (Exception e) { Console.Error.WriteLine(e.Message); Environment.Exit(1); }
Console.WriteLine($"SUMMARY: {results.Count} framework checks passed. Independent context/container and loopback HTTP experiments; not browser, database or load tests.");
if (args.Length > 0) await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new { generatedAt = DateTimeOffset.UtcNow, passed = results.Count, results }, new JsonSerializerOptions { WriteIndented = true }));

sealed class TransientProbe;
sealed class ScopedProbe;
sealed class SingletonProbe;
sealed class CaptiveProbe(ScopedProbe probe) { public ScopedProbe Captured { get; } = probe; }
