using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using SportMate.Application.Auth;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class LoginTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/auth/login";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndUser()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        var registered = await AuthApi.RegisterAsync(client, email);

        var auth = await AuthApi.LoginAsync(client, email.ToUpperInvariant());

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.Equal("Bearer", auth.TokenType);
        Assert.Equal(15 * 60, auth.ExpiresIn);
        Assert.Equal(registered.Id, auth.User.Id);
        Assert.Equal("User", auth.User.Role);
    }

    [Theory]
    [InlineData(UserRole.CourtOwner, "CourtOwner")]
    [InlineData(UserRole.Admin, "Admin")]
    public async Task Login_ReturnsPromotedRole(UserRole role, string expected)
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        var registered = await AuthApi.RegisterAsync(client, email);
        await factory.ChangeRoleAsync(registered.Id, role);

        var auth = await AuthApi.LoginAsync(client, email);

        Assert.Equal(expected, auth.User.Role);
        Assert.Equal(expected, new JsonWebToken(auth.AccessToken).GetClaim("role").Value);
    }

    [Fact]
    public async Task Login_StoresOnlyRefreshTokenHash()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.RefreshTokens.SingleAsync(t => t.UserId == auth.User.Id);
            Assert.Equal(RefreshTokenGenerator.Hash(auth.RefreshToken), stored.TokenHash);
            Assert.NotEqual(auth.RefreshToken, stored.TokenHash);
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.TokenHash == auth.RefreshToken));
        });
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        await AuthApi.RegisterAsync(client, email);

        using var response = await AuthApi.PostAsync(client, Path, new { email, password = "Wr0ngPassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithSameMessageAsWrongPassword()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        await AuthApi.RegisterAsync(client, email);

        using var wrongPassword = await AuthApi.PostAsync(client, Path, new { email, password = "Wr0ngPassword" });
        using var unknownEmail = await AuthApi.PostAsync(
            client, Path, new { email = AuthApi.UniqueEmail(), password = "Wr0ngPassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongPasswordProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var unknownEmailProblem = await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(wrongPasswordProblem!.Title, unknownEmailProblem!.Title);
    }

    [Fact]
    public async Task Login_WithSuspendedAccount_Returns403AndIssuesNoToken()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        var user = await AuthApi.RegisterAsync(client, email);
        await factory.WithDbAsync(async db =>
        {
            var entity = await db.Users.SingleAsync(u => u.Id == user.Id);
            entity.Suspend(DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        using var response = await AuthApi.PostAsync(client, Path, new { email, password = AuthApi.ValidPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await factory.WithDbAsync(async db =>
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id)));
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new { });

        await RegistrationTests.AssertValidationErrorAsync(response, "email", "password");
    }
}
