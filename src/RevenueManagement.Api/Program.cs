using RevenueManagement.Api.Common.Middleware;
using Microsoft.EntityFrameworkCore;
using RevenueManagement.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. DATABASE CONFIGURATION
// ==========================================

var databaseConnection =
    builder.Configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(databaseConnection))
{
    throw new InvalidOperationException(
        "Missing ConnectionStrings:Database. " +
        "Configure ConnectionStrings__Database " +
        "or use .NET User Secrets.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        databaseConnection,
        npgsql => npgsql.EnableRetryOnFailure()
    )
);

// ==========================================
// 2. DEPENDENCY INJECTION
// ==========================================

// Controllers for REV-02 (Reports).
builder.Services.AddControllers();

// API documentation for Minimal API and Controllers.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Example of Dependency Injection.
// Other services will be registered here later.
builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System
);

// ==========================================
// 3. CORS CONFIGURATION
// ==========================================

const string frontendCorsPolicy = "FrontendPolicy";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:Origins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ==========================================
// 4. BUILD APPLICATION
// ==========================================

var app = builder.Build();


app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
// ==========================================
// 5. MIDDLEWARE PIPELINE
// ==========================================

// Swagger is only enabled in Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors(frontendCorsPolicy);

// TODO(A): Add global exception middleware.
// TODO(A): Add JWT authentication.
// TODO(A): Add resource authorization.

// ==========================================
// 6. MAP ENDPOINTS
// ==========================================

// Report Controllers (REV-02).
app.MapControllers();

// Test API + DI.
app.MapGet("/api/ping", (TimeProvider clock) =>
{
    return Results.Ok(new
    {
        message = "Revenue Management API is running",
        status = "OK",
        utcNow = clock.GetUtcNow()
    });
})
.WithTags("System");

// Simple application health check.
// This endpoint does not verify database connectivity.
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        service = "RevenueManagement.Api"
    });
})
.WithTags("Health");

// Database health check from the original project.
app.MapGet(
    "/health/database",
    async (AppDbContext db, CancellationToken cancellationToken) =>
    {
        try
        {
            var canConnect = await db.Database
                .CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                return Results.Ok(new
                {
                    status = "Healthy",
                    database = "Neon PostgreSQL"
                });
            }

            return Results.Json(
                new
                {
                    status = "Unhealthy",
                    database = "Neon PostgreSQL"
                },
                statusCode: 503
            );
        }
        catch
        {
            return Results.Json(
                new
                {
                    status = "Unhealthy",
                    database = "Neon PostgreSQL"
                },
                statusCode: 503
            );
        }
    }
)
.WithTags("Health");

// ==========================================
// 7. START APPLICATION
// ==========================================

app.Run();

// Keep this for integration testing.
public partial class Program { }
