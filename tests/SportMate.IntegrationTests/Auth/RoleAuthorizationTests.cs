using System.Net;
using System.Net.Http.Json;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class RoleAuthorizationTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Theory]
    [InlineData(RoleProbeController.AdminPath)]
    [InlineData(RoleProbeController.CourtOwnerPath)]
    public async Task RoleProtectedEndpoint_WithoutToken_Returns401(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User, RoleProbeController.AdminPath, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.User, RoleProbeController.CourtOwnerPath, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.CourtOwner, RoleProbeController.AdminPath, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.CourtOwner, RoleProbeController.CourtOwnerPath, HttpStatusCode.OK)]
    [InlineData(UserRole.Admin, RoleProbeController.AdminPath, HttpStatusCode.OK)]
    [InlineData(UserRole.Admin, RoleProbeController.CourtOwnerPath, HttpStatusCode.Forbidden)]
    public async Task RoleProtectedEndpoint_AllowsOnlyTheRequiredRole(UserRole role, string path, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var accessToken = (await LoginWithRoleAsync(client, role)).AccessToken;

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, path, accessToken));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task NormalUserFunctionality_IsAvailableToEveryRole(UserRole role)
    {
        using var client = factory.CreateClient();
        var accessToken = (await LoginWithRoleAsync(client, role)).AccessToken;

        using var authenticated = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AuthenticatedPath, accessToken));
        using var getMe = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, "/api/v1/users/me", accessToken));
        using var putMe = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, "/api/v1/users/me", accessToken, new { display_name = "Still A Player" }));

        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getMe.StatusCode);
        Assert.Equal(HttpStatusCode.OK, putMe.StatusCode);
    }

    [Fact]
    public async Task Promotion_TakesEffectForAnExistingAccessToken()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        using var before = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AdminPath, auth.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        await factory.ChangeRoleAsync(auth.User.Id, UserRole.Admin);

        // Same token, still carrying role=User, now authorized as Admin.
        using var after = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AdminPath, auth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Demotion_TakesEffectForAnExistingAccessToken()
    {
        using var client = factory.CreateClient();
        var admin = await LoginWithRoleAsync(client, UserRole.Admin);
        var accessToken = admin.AccessToken;
        using var before = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AdminPath, accessToken));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await factory.ChangeRoleAsync(admin.User.Id, UserRole.User);

        // Same token, still carrying role=Admin, is no longer authorized as Admin.
        using var after = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AdminPath, accessToken));
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);

        using var probe = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AuthenticatedPath, accessToken));
        var body = await probe.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("User", body!["role"]);
    }

    [Fact]
    public async Task SuspendedAdmin_IsRejectedWith401()
    {
        using var client = factory.CreateClient();
        var admin = await LoginWithRoleAsync(client, UserRole.Admin);
        var accessToken = admin.AccessToken;

        await factory.SuspendAsync(admin.User.Id);

        using var response = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, RoleProbeController.AdminPath, accessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AuthApi.AuthDto> LoginWithRoleAsync(HttpClient client, UserRole role)
    {
        var email = AuthApi.UniqueEmail();
        var user = await AuthApi.RegisterAsync(client, email);
        await factory.ChangeRoleAsync(user.Id, role);
        return await AuthApi.LoginAsync(client, email);
    }
}
