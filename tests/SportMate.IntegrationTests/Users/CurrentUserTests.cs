using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Users;

public sealed class CurrentUserTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/users/me";

    [Fact]
    public async Task GetMe_WhenAuthenticated_ReturnsOnlyPublicFields()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, auth.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var fields = json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["display_name", "email", "id", "phone", "role", "status"], fields);
        Assert.Equal("User", json.RootElement.GetProperty("role").GetString());
        Assert.Equal(auth.User.Id.ToString(), json.RootElement.GetProperty("id").GetString());
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenAccountSuspendedAfterLogin_Returns401()
    {
        // An access token issued before suspension is no longer accepted (business rule 1.2).
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.SuspendAsync(auth.User.Id);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, auth.AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsCurrentRoleAfterPromotion()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.ChangeRoleAsync(auth.User.Id, UserRole.CourtOwner);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, auth.AccessToken));

        var user = await response.Content.ReadFromJsonAsync<AuthApi.UserDto>(AuthApi.Json);
        Assert.Equal("CourtOwner", user!.Role);
    }
}
