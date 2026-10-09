using RevenueManagement.Api.Features.Reports;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RevenueManagement.Api.Common.Middleware;
using RevenueManagement.Api.Domain.Entities;
using RevenueManagement.Api.Features.Auth;
using RevenueManagement.Api.Infrastructure.Authorization;
using RevenueManagement.Api.Infrastructure.Persistence;
using RevenueManagement.Api.Options;

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
// 2. DEPENDENCY INJECTION & JWT AUTH
// ==========================================

// Controllers for REV-02 (Reports) and Auth.
builder.Services.AddControllers();

// API documentation for Minimal API and Controllers.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DI services
builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System
);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<CurrentUser>();

builder.Services.AddScoped<
    IAuthorizationHandler,
    GroupAuthorizationHandler>();

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddScoped<AuthService>();

// JWT Options & Authentication
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "Missing Jwt configuration.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Missing Jwt:SigningKey.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtOptions.SigningKey)),

                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = "sub"
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.GroupMember,
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.AddRequirements(
                new GroupMemberRequirement());
        });
});

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

builder.Services.AddRevenueReports(builder.Configuration, databaseConnection);
var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ReportRequestLoggingMiddleware>();

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

app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 6. MAP ENDPOINTS
// ==========================================

// Report Controllers (REV-02) and Auth Controllers.
app.MapControllers();

// Test Group Authorization & IDOR endpoint.
app.MapGet(
    "/api/auth-test/groups/{groupId:long}",
    async (
        long groupId,
        HttpContext httpContext,
        IAuthorizationService authorizationService) =>
    {
        var result =
            await authorizationService.AuthorizeAsync(
                httpContext.User,
                groupId,
                AuthorizationPolicies.GroupMember);

        if (!result.Succeeded)
        {
            return Results.Forbid();
        }

        return Results.Ok(new
        {
            message = "You can access this group.",
            groupId
        });
    })
    .RequireAuthorization();

// Test JWT Auth endpoints.
app.MapGet("/api/auth-test/public", () =>
{
    return Results.Ok(new
    {
        message = "Public endpoint"
    });
});

app.MapGet("/api/auth-test/private", () =>
{
    return Results.Ok(new
    {
        message = "Authenticated endpoint"
    });
})
.RequireAuthorization();

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

app.MapHealthChecks("/health/redis");
app.Run();

// Keep this for integration testing.
public partial class Program { }
