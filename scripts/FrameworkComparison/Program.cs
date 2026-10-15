using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Use(async (context, next) => { context.Response.Headers["X-Lab-Trace"] = "aspnetcore"; await next(context); });
app.MapControllers();
await app.RunAsync();

public sealed class ComparisonInput
{
    [Range(1, 1_000_000)] public int Amount { get; set; }
}
[ApiController, Route("comparison")]
public sealed class ComparisonController : ControllerBase
{
    [HttpPost("validate")] public IActionResult Validate(ComparisonInput input) => Ok(new { amount = input.Amount });
}
