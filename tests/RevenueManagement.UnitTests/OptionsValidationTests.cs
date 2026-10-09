using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RevenueManagement.Api.Options;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class OptionsValidationTests
{
    private static IServiceProvider BuildServiceProvider(Dictionary<string, string?> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddAppOptions(configuration);

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> CreateValidConfig() => new()
    {
        ["Jwt:Issuer"] = "RevenueManagement",
        ["Jwt:Audience"] = "RevenueManagement.Web",
        ["Jwt:SigningKey"] = "this-is-a-very-long-secret-key-32-chars-minimum",
        ["Jwt:ExpiryMinutes"] = "60",
        ["Jwt:RefreshExpiryDays"] = "7",

        ["Redis:ConnectionString"] = "localhost:6379",
        ["Redis:ReportTtlSeconds"] = "120",
        ["Redis:InstanceName"] = "RevenueManagement:",

        ["Worker:Enabled"] = "true",
        ["Worker:PollIntervalSeconds"] = "30",
        ["Worker:MaxRetryAttempts"] = "3",
        ["Worker:BaseRetryDelaySeconds"] = "2",
        ["Worker:MaxRetryDelaySeconds"] = "60",
        ["Worker:QueueCapacity"] = "500",
        ["Worker:ScanBatchSize"] = "200",
        ["Worker:MaxCatchUpOccurrences"] = "31"
    };

    [Fact]
    public void AddAppOptions_WithValidConfiguration_Succeeds()
    {
        var provider = BuildServiceProvider(CreateValidConfig());

        var jwt = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var redis = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
        var worker = provider.GetRequiredService<IOptions<WorkerOptions>>().Value;

        Assert.Equal("RevenueManagement", jwt.Issuer);
        Assert.Equal("RevenueManagement.Web", jwt.Audience);
        Assert.Equal("this-is-a-very-long-secret-key-32-chars-minimum", jwt.SigningKey);
        Assert.Equal(60, jwt.ExpiryMinutes);
        Assert.Equal(7, jwt.RefreshExpiryDays);

        Assert.Equal("localhost:6379", redis.ConnectionString);
        Assert.Equal(120, redis.ReportTtlSeconds);

        Assert.True(worker.Enabled);
        Assert.Equal(30, worker.PollIntervalSeconds);
        Assert.Equal(3, worker.MaxRetryAttempts);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:SigningKey", "")]
    [InlineData("Jwt:SigningKey", "too-short-key")]
    [InlineData("Jwt:ExpiryMinutes", "0")]
    [InlineData("Jwt:ExpiryMinutes", "2000")]
    [InlineData("Jwt:RefreshExpiryDays", "0")]
    [InlineData("Jwt:RefreshExpiryDays", "100")]
    public void JwtOptions_WithInvalidValue_ThrowsOptionsValidationException(string key, string invalidValue)
    {
        var config = CreateValidConfig();
        config[key] = invalidValue;

        var provider = BuildServiceProvider(config);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<JwtOptions>>().Value);

        Assert.NotEmpty(exception.Failures);
    }

    [Theory]
    [InlineData("Redis:ConnectionString", "")]
    [InlineData("Redis:ReportTtlSeconds", "4")]
    [InlineData("Redis:ReportTtlSeconds", "4000")]
    public void RedisOptions_WithInvalidValue_ThrowsOptionsValidationException(string key, string invalidValue)
    {
        var config = CreateValidConfig();
        config[key] = invalidValue;

        var provider = BuildServiceProvider(config);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<RedisOptions>>().Value);

        Assert.NotEmpty(exception.Failures);
    }

    [Theory]
    [InlineData("Worker:PollIntervalSeconds", "0")]
    [InlineData("Worker:PollIntervalSeconds", "5000")]
    [InlineData("Worker:MaxRetryAttempts", "-1")]
    [InlineData("Worker:MaxRetryAttempts", "15")]
    [InlineData("Worker:BaseRetryDelaySeconds", "0")]
    [InlineData("Worker:MaxRetryDelaySeconds", "0")]
    [InlineData("Worker:QueueCapacity", "0")]
    [InlineData("Worker:ScanBatchSize", "0")]
    [InlineData("Worker:MaxCatchUpOccurrences", "0")]
    [InlineData("Worker:MaxCatchUpOccurrences", "400")]
    public void WorkerOptions_WithInvalidValue_ThrowsOptionsValidationException(string key, string invalidValue)
    {
        var config = CreateValidConfig();
        config[key] = invalidValue;

        var provider = BuildServiceProvider(config);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<WorkerOptions>>().Value);

        Assert.NotEmpty(exception.Failures);
    }
}
