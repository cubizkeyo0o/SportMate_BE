using System.Net;
using Microsoft.EntityFrameworkCore;
using SportMate.Application.Auth;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class LogoutTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/auth/logout";

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndItCannotBeReused()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var logout = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Post, Path, auth.AccessToken, new { refresh_token = auth.RefreshToken }));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var token = await db.RefreshTokens.SingleAsync(t => t.TokenHash == RefreshTokenGenerator.Hash(auth.RefreshToken));
            Assert.NotNull(token.RevokedAtUtc);
        });

        using var refresh = await AuthApi.PostAsync(
            client, "/api/v1/auth/refresh", new { refresh_token = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAccessToken_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await AuthApi.PostAsync(client, Path, new { refresh_token = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithTokenOfSuspendedUser_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.SuspendAsync(auth.User.Id);

        using var response = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Post, Path, auth.AccessToken, new { refresh_token = auth.RefreshToken }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_CannotRevokeAnotherUsersRefreshToken()
    {
        using var client = factory.CreateClient();
        var victim = await AuthApi.RegisterAndLoginAsync(client);
        var attacker = await AuthApi.RegisterAndLoginAsync(client);

        using var logout = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Post, Path, attacker.AccessToken, new { refresh_token = victim.RefreshToken }));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var refresh = await AuthApi.PostAsync(
            client, "/api/v1/auth/refresh", new { refresh_token = victim.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithMissingRefreshToken_Returns400()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Post, Path, auth.AccessToken, new { }));

        await RegistrationTests.AssertValidationErrorAsync(response, "refresh_token");
    }
}
