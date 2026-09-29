using System.Net;
using System.Text.Json;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests;

public sealed class SwaggerTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task SwaggerDocument_ExposesBearerSchemeOnProtectedEndpointsOnly()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(RequiresBearer(paths, "/api/v1/users/me", "get"));
        Assert.True(RequiresBearer(paths, "/api/v1/users/me", "put"));
        Assert.True(RequiresBearer(paths, "/api/v1/auth/logout", "post"));
        Assert.True(RequiresBearer(paths, "/api/v1/courts", "post"));
        Assert.True(RequiresBearer(paths, "/api/v1/courts/{id}", "get"));
        Assert.True(RequiresBearer(paths, "/api/v1/courts/{id}", "put"));
        Assert.False(RequiresBearer(paths, "/api/v1/auth/login", "post"));
        Assert.False(RequiresBearer(paths, "/api/v1/auth/register", "post"));
        Assert.False(RequiresBearer(paths, "/api/v1/auth/refresh", "post"));
    }

    private static bool RequiresBearer(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).TryGetProperty("security", out var security)
        && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));
}
