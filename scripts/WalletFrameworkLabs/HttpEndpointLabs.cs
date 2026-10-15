using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

static class HttpEndpointLabs
{
    public static async Task Run(Action<bool, string> check)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(LabComparisonController).Assembly.FullName });
        builder.Logging.ClearProviders();
        builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(LabComparisonController).Assembly);
        builder.Services.AddScoped<LabActionFilter>();
        builder.Services.AddScoped<HttpScopedProbe>();
        builder.Services.AddTransient<HttpTransientProbe>();
        builder.Services.AddSingleton<HttpSingletonProbe>();
        await using var app = builder.Build();
        app.Use(async (context, next) => { context.Response.Headers["X-Lab-Middleware"] = "executed"; await next(context); });
        app.MapControllers();
        app.MapPost("/lab/minimal", (LabInput input) => Results.Ok(new { amount = input.Amount }))
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers["X-Lab-Endpoint-Filter"] = "executed";
                var input = context.GetArgument<LabInput>(0);
                if (input is null || input.Amount <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = [input is null ? "Request body is required" : "Amount must be positive"] });
                return await next(context);
            });
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
            using var controllerOk = await client.PostAsJsonAsync("/lab/controller", new { amount = 10 });
            check(controllerOk.StatusCode == HttpStatusCode.OK && (await controllerOk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("amount").GetInt32() == 10, "Controller binds valid JSON and returns HTTP 200");
            check(controllerOk.Headers.Contains("X-Lab-Middleware") && controllerOk.Headers.Contains("X-Lab-Action-Filter") && !controllerOk.Headers.Contains("X-Lab-Endpoint-Filter"), "MVC action filter runs inside the common middleware without endpoint filter");
            using var controllerBad = await client.PostAsJsonAsync("/lab/controller", new { amount = 0 });
            check(controllerBad.StatusCode == HttpStatusCode.BadRequest && (await controllerBad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("Amount", out _), "ApiController DataAnnotations reject invalid bound DTO");
            using var broken = await client.PostAsync("/lab/controller", new StringContent("{broken", Encoding.UTF8, "application/json"));
            check(broken.StatusCode == HttpStatusCode.BadRequest, "MVC malformed JSON is rejected before action execution");
            using var minimalOk = await client.PostAsJsonAsync("/lab/minimal", new { amount = 10 });
            check(minimalOk.StatusCode == HttpStatusCode.OK && (await minimalOk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("amount").GetInt32() == 10, "Minimal API binds the same DTO and returns HTTP 200");
            check(minimalOk.Headers.Contains("X-Lab-Middleware") && minimalOk.Headers.Contains("X-Lab-Endpoint-Filter") && !minimalOk.Headers.Contains("X-Lab-Action-Filter"), "Endpoint filter runs for Minimal API without MVC action filter");
            using var minimalBad = await client.PostAsJsonAsync("/lab/minimal", new { amount = 0 });
            check(minimalBad.StatusCode == HttpStatusCode.BadRequest, "Explicit endpoint filter rejects invalid Minimal API input");
            using var missing = await client.PostAsync("/lab/minimal", new StringContent("", Encoding.UTF8, "application/json"));
            check(missing.StatusCode == HttpStatusCode.BadRequest, "Minimal API filter rejects a missing JSON body without null dereference");
            var first = await client.GetFromJsonAsync<JsonElement>("/lab/controller/lifetimes");
            var second = await client.GetFromJsonAsync<JsonElement>("/lab/controller/lifetimes");
            check(first.GetProperty("scopedId").GetString() == first.GetProperty("sameScopedId").GetString(), "HTTP request shares scoped dependency within its request scope");
            check(first.GetProperty("scopedId").GetString() != second.GetProperty("scopedId").GetString(), "Two HTTP requests receive different scoped dependency IDs");
            check(first.GetProperty("singletonId").GetString() == second.GetProperty("singletonId").GetString(), "Two HTTP requests share the singleton dependency ID");
            check(first.GetProperty("transientId").GetString() != first.GetProperty("otherTransientId").GetString(), "Two resolutions within one HTTP request create different transient IDs");
        }
        finally { await app.StopAsync(); }
    }
}

public sealed class LabInput { [Range(1, int.MaxValue)] public int Amount { get; set; } }
public sealed class HttpScopedProbe { public Guid Id { get; } = Guid.NewGuid(); }
public sealed class HttpTransientProbe { public Guid Id { get; } = Guid.NewGuid(); }
public sealed class HttpSingletonProbe { public Guid Id { get; } = Guid.NewGuid(); }
public sealed class LabActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    { context.HttpContext.Response.Headers["X-Lab-Action-Filter"] = "executed"; await next(); }
}
[ApiController, Route("lab/controller"), ServiceFilter(typeof(LabActionFilter))]
public sealed class LabComparisonController : ControllerBase
{
    [HttpPost] public IActionResult Post(LabInput input) => Ok(new { amount = input.Amount });
    [HttpGet("lifetimes")]
    public IActionResult Lifetimes([FromServices] HttpScopedProbe scoped, [FromServices] HttpSingletonProbe singleton, [FromServices] HttpTransientProbe transient)
        => Ok(new { scopedId = scoped.Id, sameScopedId = HttpContext.RequestServices.GetRequiredService<HttpScopedProbe>().Id, singletonId = singleton.Id, transientId = transient.Id, otherTransientId = HttpContext.RequestServices.GetRequiredService<HttpTransientProbe>().Id });
}
