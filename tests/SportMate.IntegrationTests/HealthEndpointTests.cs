using System.Net;
using System.Text.Json;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests;

public sealed class HealthEndpointTests(SportMateApiFactory factory)
    : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task GetHealth_ReturnsOkStatus()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
    }
}