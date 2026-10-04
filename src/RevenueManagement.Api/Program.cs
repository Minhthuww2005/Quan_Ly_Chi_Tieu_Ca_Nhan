using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RevenueManagement.Api.Features.Reports;
using RevenueManagement.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var databaseConnection = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(databaseConnection))
    throw new InvalidOperationException(
        "Thiếu ConnectionStrings:Database. Hãy cấu hình bằng environment variable " +
        "ConnectionStrings__Database hoặc .NET User Secrets.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseConnection, npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRevenueReports(builder.Configuration, databaseConnection);

// TODO(A): đăng ký middleware, auth và các feature services tại đây.

var app = builder.Build();

// TODO(A): cấu hình pipeline đúng thứ tự theo checklist 1A.1.
app.UseMiddleware<ReportRequestLoggingMiddleware>();

app.MapGet("/", () => "Revenue Management API skeleton");
app.MapGet("/health/database", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "Healthy", database = "Neon PostgreSQL" })
            : Results.Json(new { status = "Unhealthy", database = "Neon PostgreSQL" }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { status = "Unhealthy", database = "Neon PostgreSQL" }, statusCode: 503);
    }
});
app.MapControllers();
app.MapHealthChecks("/health/redis");
app.Run();

public partial class Program { }

