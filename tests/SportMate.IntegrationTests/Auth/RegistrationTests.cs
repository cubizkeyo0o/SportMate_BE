using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Auth;

public sealed class RegistrationTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/auth/register";

    [Fact]
    public async Task Register_WithValidInput_Returns201AndNormalizedUser()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email = "  " + email.ToUpperInvariant() + " ",
            password = AuthApi.ValidPassword,
            display_name = "  Minh Anh  ",
            phone = "+84 912 345 678"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/users/me", response.Headers.Location?.OriginalString);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
        Assert.Equal("Minh Anh", json.RootElement.GetProperty("display_name").GetString());
        Assert.Equal("+84 912 345 678", json.RootElement.GetProperty("phone").GetString());
        Assert.Equal("active", json.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_StoresHashedPasswordOnly()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();

        await AuthApi.RegisterAsync(client, email);

        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.NotEqual(AuthApi.ValidPassword, user.PasswordHash);
            Assert.DoesNotContain(AuthApi.ValidPassword, user.PasswordHash);
        });
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        using var client = factory.CreateClient();
        var email = AuthApi.UniqueEmail();
        await AuthApi.RegisterAsync(client, email);

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email = email.ToUpperInvariant(),
            password = AuthApi.ValidPassword,
            display_name = "Someone Else"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("user@localhost")]
    [InlineData("Name <user@example.com>")]
    [InlineData("")]
    public async Task Register_WithInvalidEmail_Returns400(string email)
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email,
            password = AuthApi.ValidPassword,
            display_name = "Test"
        });

        await AssertValidationErrorAsync(response, "email");
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("1234567890")]
    [InlineData("")]
    public async Task Register_WithInvalidPassword_Returns400(string password)
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email = AuthApi.UniqueEmail(),
            password,
            display_name = "Test"
        });

        await AssertValidationErrorAsync(response, "password");
    }

    [Fact]
    public async Task Register_WithoutDisplayName_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email = AuthApi.UniqueEmail(),
            password = AuthApi.ValidPassword
        });

        await AssertValidationErrorAsync(response, "display_name");
    }

    [Fact]
    public async Task Register_WithTooLongDisplayNameAndInvalidPhone_ReturnsBothErrors()
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, Path, new
        {
            email = AuthApi.UniqueEmail(),
            password = AuthApi.ValidPassword,
            display_name = new string('a', 101),
            phone = "call me"
        });

        await AssertValidationErrorAsync(response, "display_name", "phone");
    }

    internal static async Task AssertValidationErrorAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        foreach (var field in fields)
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Expected a validation error for '{field}'.");
        }
    }
}
