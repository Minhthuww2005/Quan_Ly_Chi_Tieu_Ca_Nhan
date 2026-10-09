using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RevenueManagement.Api.Features.Auth;
using RevenueManagement.Api.Features.Revenues;
using Xunit;

namespace RevenueManagement.IntegrationTests;

public sealed class RevenueApiTests
{
    [Fact]
    public async Task ConcurrentRefresh_ConsumesTokenOnlyOnce()
    {
        var url = Environment.GetEnvironmentVariable("REVENUEFLOW_TEST_API");
        if (string.IsNullOrWhiteSpace(url)) Assert.Skip("Requires isolated demo API.");
        using var client = new HttpClient { BaseAddress = new Uri(url!) };
        var token = await Login(client, "demo2@revenueflow.local");
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(token.RefreshToken), TestContext.Current.CancellationToken),
            client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(token.RefreshToken), TestContext.Current.CancellationToken));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
        foreach (var response in responses) response.Dispose();
    }
    [Fact]
    public async Task DemoApi_CrudAuthorizationValidationAndCacheInvalidation()
    {
        var url = Environment.GetEnvironmentVariable("REVENUEFLOW_TEST_API");
        if (string.IsNullOrWhiteSpace(url)) Assert.Skip("Requires a running, isolated demo API in REVENUEFLOW_TEST_API.");
        using var client = new HttpClient { BaseAddress = new Uri(url!) };
        var unauthorized = await client.GetAsync("/api/revenues?groupId=1");
        await Error(unauthorized, HttpStatusCode.Unauthorized);
        var token = await Login(client,"demo1@revenueflow.local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var context = await client.GetFromJsonAsync<JsonElement>("/api/context");
        var group = context.GetProperty("groups")[0].GetProperty("groupId").GetInt64();
        var wallet = context.GetProperty("wallets")[0].GetProperty("id").GetInt64();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var reportUrl = $"/api/reports/revenue?groupId={group}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}";
        var invalid = await client.PostAsJsonAsync("/api/revenues",new CreateRevenueRequest(wallet,null,0,date,"invalid"));
        await Error(invalid,HttpStatusCode.BadRequest);
        await Error(await client.GetAsync("/api/revenues/9223372036854775807"),HttpStatusCode.NotFound);
        var createdResponse = await client.PostAsJsonAsync("/api/revenues",new CreateRevenueRequest(wallet,null,12345,date,"HTTP integration test"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var item = (await createdResponse.Content.ReadFromJsonAsync<RevenueResponse>())!;
        try
        {
            var first = await client.GetAsync(reportUrl);
            var second = await client.GetAsync(reportUrl);
            Assert.Equal("HIT",second.Headers.GetValues("X-Report-Cache").Single());
            var updated = await client.PutAsJsonAsync($"/api/revenues/{item.Id}",new UpdateRevenueRequest(wallet,null,54321,date,"edited",item.RowVersion));
            Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
            var fresh = await client.GetAsync(reportUrl);
            Assert.Equal("MISS",fresh.Headers.GetValues("X-Report-Cache").Single());
            var stale = await client.PutAsJsonAsync($"/api/revenues/{item.Id}",new UpdateRevenueRequest(wallet,null,999,date,"stale",item.RowVersion));
            await Error(stale,HttpStatusCode.Conflict);
            using var outsider = new HttpClient { BaseAddress = client.BaseAddress };
            outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",(await Login(outsider,"demo3@revenueflow.local")).AccessToken);
            await Error(await outsider.GetAsync($"/api/revenues/{item.Id}"),HttpStatusCode.Forbidden);
            await Error(await outsider.GetAsync(reportUrl),HttpStatusCode.Forbidden);
        }
        finally { Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/revenues/{item.Id}")).StatusCode); }
        await Error(await client.GetAsync($"/api/revenues/{item.Id}"),HttpStatusCode.NotFound);
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh",new RefreshTokenRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.OK,refreshed.StatusCode);
        await Error(await client.PostAsJsonAsync("/api/auth/refresh",new RefreshTokenRequest(token.RefreshToken)),HttpStatusCode.Unauthorized);
    }

    private static async Task<TokenResponse> Login(HttpClient client,string email)
    {
        var r = await client.PostAsJsonAsync("/api/auth/login",new LoginRequest(email,"Demo@123456"));
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
    private static async Task Error(HttpResponseMessage response,HttpStatusCode expected)
    {
        Assert.Equal(expected,response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("requestId").GetString()));
    }
}
