using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// Thin HTTP helpers for the auth endpoints. Responses are read with the API's snake_case contract.
/// </summary>
public static class AuthApi
{
    public const string ValidPassword = "Str0ngPassw0rd";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(path, body, Json);

    public static async Task<UserDto> RegisterAsync(HttpClient client, string email, string password = ValidPassword)
    {
        using var response = await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            password,
            display_name = "Test Player"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserDto>(Json))!;
    }

    public static async Task<AuthDto> LoginAsync(HttpClient client, string email, string password = ValidPassword)
    {
        using var response = await PostAsync(client, "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthDto>(Json))!;
    }

    public static async Task<AuthDto> RegisterAndLoginAsync(HttpClient client)
    {
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        return await LoginAsync(client, email);
    }

    public static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    public sealed record UserDto(Guid Id, string Email, string DisplayName, string? Phone, string Status, string Role);

    public sealed record AuthDto(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn,
        UserDto User);
}
