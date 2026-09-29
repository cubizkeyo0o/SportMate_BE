using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class RefreshTokenTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/auth/refresh";

    [Fact]
    public async Task Refresh_WithValidToken_IssuesNewPairAndRevokesOld()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        var refreshed = await RefreshAsync(client, auth.RefreshToken);

        Assert.NotEqual(auth.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(auth.AccessToken, refreshed.AccessToken);
        Assert.Equal(auth.User.Id, refreshed.User.Id);
        Assert.Equal("User", refreshed.User.Role);
        await factory.WithDbAsync(async db =>
        {
            var old = await db.RefreshTokens.SingleAsync(t => t.TokenHash == RefreshTokenGenerator.Hash(auth.RefreshToken));
            var current = await db.RefreshTokens.SingleAsync(t => t.TokenHash == RefreshTokenGenerator.Hash(refreshed.RefreshToken));
            Assert.NotNull(old.RevokedAtUtc);
            Assert.Null(current.RevokedAtUtc);
        });

        using var me = await client.SendAsync(
            AuthApi.Authorized(HttpMethod.Get, "/api/v1/users/me", refreshed.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithOldTokenAfterRotation_Returns401AndRevokesTheWholeFamily()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var rotated = await RefreshAsync(client, auth.RefreshToken);

        using var reuse = await PostRefreshAsync(client, auth.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Reuse of a rotated token signals theft, so the token issued by the rotation is revoked too.
        using var afterReuse = await PostRefreshAsync(client, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReturnsCurrentRole()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.ChangeRoleAsync(auth.User.Id, UserRole.Admin);

        var refreshed = await RefreshAsync(client, auth.RefreshToken);

        Assert.Equal("Admin", refreshed.User.Role);
        Assert.Equal("Admin", new JsonWebToken(refreshed.AccessToken).GetClaim("role").Value);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var rawToken = RefreshTokenGenerator.Generate();
        var now = DateTime.UtcNow;
        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.Add(new RefreshToken(
                auth.User.Id, RefreshTokenGenerator.Hash(rawToken), expiresAtUtc: now.AddMinutes(-1), utcNow: now.AddDays(-30)));
            await db.SaveChangesAsync();
        });

        using var response = await PostRefreshAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.WithDbAsync(async db =>
        {
            var token = await db.RefreshTokens.SingleAsync(t => t.UserId == auth.User.Id);
            token.Revoke(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        using var response = await PostRefreshAsync(client, auth.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("definitely-not-a-real-refresh-token")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.e30.signature")]
    public async Task Refresh_WithUnknownToken_Returns401(string token)
    {
        using var client = factory.CreateClient();

        using var response = await PostRefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAccessTokenInsteadOfRefreshToken_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PostRefreshAsync(client, auth.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithMissingToken_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new { });

        await RegistrationTests.AssertValidationErrorAsync(response, "refresh_token");
    }

    [Fact]
    public async Task Refresh_ForSuspendedUser_Returns401()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).Suspend(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        using var response = await PostRefreshAsync(client, auth.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostRefreshAsync(HttpClient client, string refreshToken) =>
        AuthApi.PostAsync(client, Path, new { refresh_token = refreshToken });

    private static async Task<AuthApi.AuthDto> RefreshAsync(HttpClient client, string refreshToken)
    {
        using var response = await PostRefreshAsync(client, refreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthApi.AuthDto>(AuthApi.Json))!;
    }
}
