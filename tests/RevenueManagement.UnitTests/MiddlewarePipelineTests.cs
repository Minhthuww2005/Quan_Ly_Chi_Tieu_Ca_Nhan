using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using RevenueManagement.Api.Common.Middleware;
using RevenueManagement.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class MiddlewarePipelineTests
{
    [Fact]
    public async Task ExceptionHandlerBeforeFailingMiddleware_Produces500Contract()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(_ => throw new InvalidOperationException("Controlled pipeline demo"), NullLogger<ExceptionMiddleware>.Instance);
        await new RequestIdMiddleware(middleware.InvokeAsync).InvokeAsync(context);
        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("internal_error",body);
        Assert.Contains(context.TraceIdentifier,body);
        Assert.DoesNotContain("Controlled pipeline demo",body);
    }
    [Fact]
    public async Task FailingMiddlewareBeforeExceptionHandler_EscapesHandler()
    {
        var handlerEntered = false;
        var handler = new ExceptionMiddleware(_ => { handlerEntered = true; return Task.CompletedTask; }, NullLogger<ExceptionMiddleware>.Instance);
        RequestDelegate incorrectlyOrdered = async context =>
        {
            if (!handlerEntered) throw new InvalidOperationException("Throw before handler is entered");
            await handler.InvokeAsync(context);
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => incorrectlyOrdered(new DefaultHttpContext()));
        Assert.False(handlerEntered);
    }
    [Fact]
    public void SingletonCannotCaptureScopedDbContext_WhenScopeValidationEnabled()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<InvalidSingleton>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<InvalidSingleton>());
    }
    private sealed class InvalidSingleton(AppDbContext db) { public AppDbContext Database { get; } = db; }
}
