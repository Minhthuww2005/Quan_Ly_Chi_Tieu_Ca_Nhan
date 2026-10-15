using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
builder.Services.AddScoped<ScopeMarker>();
builder.Services.AddTransient<TransientMarker>();
builder.Services.AddSingleton<SingletonMarker>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    Console.WriteLine("outer:enter");
    try { await next(context); }
    finally { Console.WriteLine("outer:exit"); }
});
app.Use(async (context, next) =>
{
    Console.WriteLine("inner:enter");
    try { await next(context); }
    finally { Console.WriteLine("inner:exit"); }
});
app.MapGet("/scope", (ScopeMarker first,
    IServiceProvider services,
    TransientMarker transient,
    SingletonMarker singleton) => Results.Ok(new
{
    scoped = first.Id,
    sameScope = first.Id == services.GetRequiredService<ScopeMarker>().Id,
    transient = transient.Id,
    secondTransient = services.GetRequiredService<TransientMarker>().Id,
    singleton = singleton.Id
}));
app.MapPost("/amount", ([FromBody] AmountRequest? input) =>
{
    if (input is null) return Results.BadRequest(new { code = "BODY_REQUIRED" });
    var errors = new List<ValidationResult>();
    var valid = Validator.TryValidateObject(input,
        new ValidationContext(input), errors, true);
    return valid ? Results.Ok(new { amount = input.Amount })
        : Results.BadRequest(new { code = "INVALID_AMOUNT",
            errors = errors.Select(x => x.ErrorMessage) });
});
app.MapGet("/boom", IResult () =>
    throw new InvalidOperationException("LAB_INTERNAL"));
app.Run();

sealed class ScopeMarker { public Guid Id { get; } = Guid.NewGuid(); }
sealed class TransientMarker { public Guid Id { get; } = Guid.NewGuid(); }
sealed class SingletonMarker { public Guid Id { get; } = Guid.NewGuid(); }
sealed record AmountRequest([property: Range(1, 1_000_000)] int Amount);