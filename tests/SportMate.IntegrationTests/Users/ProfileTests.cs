using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Auth;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Users;

public sealed class ProfileTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/users/me";

    [Fact]
    public async Task UpdateMe_WithValidInput_UpdatesProfile()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, Path, auth.AccessToken, new { display_name = "  New Name ", phone = "0912345678" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthApi.UserDto>(AuthApi.Json);
        Assert.Equal("New Name", user!.DisplayName);
        Assert.Equal("0912345678", user.Phone);

        using var me = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, auth.AccessToken));
        var reloaded = await me.Content.ReadFromJsonAsync<AuthApi.UserDto>(AuthApi.Json);
        Assert.Equal("New Name", reloaded!.DisplayName);
        Assert.Equal("0912345678", reloaded.Phone);
    }

    [Fact]
    public async Task UpdateMe_WithoutPhone_ClearsPhone()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        using var _ = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, Path, auth.AccessToken, new { display_name = "Name", phone = "0912345678" }));

        using var response = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, Path, auth.AccessToken, new { display_name = "Name" }));

        var user = await response.Content.ReadFromJsonAsync<AuthApi.UserDto>(AuthApi.Json);
        Assert.Null(user!.Phone);
    }

    [Theory]
    [InlineData("", null, "display_name")]
    [InlineData("   ", null, "display_name")]
    [InlineData("Valid Name", "abc", "phone")]
    [InlineData("Valid Name", "123", "phone")]
    [InlineData("Valid Name", "+1234567890123456", "phone")]
    public async Task UpdateMe_WithInvalidInput_Returns400(string displayName, string? phone, string field)
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, Path, auth.AccessToken, new { display_name = displayName, phone }));

        await RegistrationTests.AssertValidationErrorAsync(response, field);
    }

    [Fact]
    public async Task UpdateMe_IgnoresProtectedFields()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        var registered = await AuthApi.RegisterAsync(client, email);
        var auth = await AuthApi.LoginAsync(client, email);
        string originalHash = null!;
        await factory.WithDbAsync(async db =>
            originalHash = (await db.Users.SingleAsync(u => u.Id == registered.Id)).PasswordHash);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Put, Path, auth.AccessToken, new
        {
            display_name = "Renamed",
            id = Guid.NewGuid(),
            email = "attacker@example.com",
            status = "suspended",
            role = "admin",
            password_hash = "injected",
            password = "N3wPassword"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == registered.Id);
            Assert.Equal("Renamed", user.DisplayName);
            Assert.Equal(email, user.Email);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal(UserRole.User, user.Role);
            Assert.Equal(originalHash, user.PasswordHash);
        });
        await AuthApi.LoginAsync(client, email);
    }

    [Fact]
    public async Task UpdateMe_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(Path, new { display_name = "Name" }, AuthApi.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
