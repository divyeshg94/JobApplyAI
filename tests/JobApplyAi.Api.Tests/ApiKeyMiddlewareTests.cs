using System.Net;
using JobApplyAi.Api.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace JobApplyAi.Api.Tests;

public class ApiKeyMiddlewareTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestKey = "test-secret-key";

    private HttpClient CreateClient(string? configuredKey = TestKey)
        => factory.WithWebHostBuilder(builder => builder.UseSetting(ApiKeyMiddleware.ConfigKey, configuredKey))
            .CreateClient();

    [Fact]
    public async Task Health_is_exempt_from_api_key()
    {
        using var response = await CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_without_key_returns_401()
    {
        using var response = await CreateClient().GetAsync("/api/subscriptions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_with_wrong_key_returns_401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyMiddleware.HeaderName, "wrong-key");
        using var response = await client.GetAsync("/api/subscriptions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_with_unconfigured_key_fails_closed_with_503()
    {
        var client = CreateClient(configuredKey: null);
        client.DefaultRequestHeaders.Add(ApiKeyMiddleware.HeaderName, "anything");
        using var response = await client.GetAsync("/api/subscriptions");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
