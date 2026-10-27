using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using WalletFunding.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
var connection = builder.Configuration.GetConnectionString("Database");
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Configure ConnectionStrings__Database and Jwt__SigningKey (32+ characters) through environment or User Secrets.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddScoped<IWalletRepository, PostgresWalletRepository>();
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (allowedOrigins.Length > 0)
    builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<BankingService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<GroupExpenseService>();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Cache:Redis"] ?? "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,asyncTimeout=5000";
    options.InstanceName = GroupCacheNamespace.ForDatabase(connection);
});
builder.Services.AddSingleton<IGroupReportCache, GroupReportCache>();
builder.Services.AddSingleton<GroupEventQueue>();
builder.Services.AddHostedService<GroupEventWorker>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IVnpayGateway, VnpayGateway>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("GoogleMetadata", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
builder.Services.AddDataProtection().SetApplicationName("HTDPay.Core")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".wallet-keys")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/groups") && !string.IsNullOrEmpty(context.Request.Query["access_token"]))
                context.Token = context.Request.Query["access_token"];
            return Task.CompletedTask;
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateIssuer = true, ValidIssuer = "WalletFunding", ValidateAudience = true, ValidAudience = "WalletFunding.Web",
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => o.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
    context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
await PostgresWalletRepository.InitializeGroupExpenses(app.Services.GetRequiredService<NpgsqlDataSource>());
if (app.Configuration.GetValue<bool>("Database:Initialize"))
    await PostgresWalletRepository.Initialize(app.Services.GetRequiredService<NpgsqlDataSource>(), app.Configuration["Database:ImportPath"], app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("SensitiveFields.v1"));
if (args.Contains("--initialize-only")) return;
app.UseMiddleware<RequestTraceMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
if (allowedOrigins.Length > 0) app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapGet("/api/health", async (NpgsqlDataSource source) =>
{
    await using var c = await source.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("SELECT count(*) FROM htdpay.schema_versions", c);
    await cmd.ExecuteScalarAsync();
    return Results.Ok(new { status = "ok", backend = "ASP.NET Core 10", database = "PostgreSQL", timestamp = DateTimeOffset.UtcNow });
});
app.MapControllers();
app.MapHub<GroupExpensesHub>("/hubs/groups", options => options.CloseOnAuthenticationExpiration = true);
await app.RunAsync();
