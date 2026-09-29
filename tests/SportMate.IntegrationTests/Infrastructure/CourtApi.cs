using System.Net;
using System.Net.Http.Json;
using SportMate.Domain.Enums;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// HTTP helpers for the court endpoints and for logging in as a user with a given role.
/// </summary>
public static class CourtApi
{
    public const string BasePath = "/api/v1/courts";

    public static string PathOf(Guid courtId) => $"{BasePath}/{courtId}";

    public static async Task<AuthApi.AuthDto> LoginWithRoleAsync(
        SportMateApiFactory factory,
        HttpClient client,
        UserRole role)
    {
        var email = AuthApi.UniqueEmail();
        var user = await AuthApi.RegisterAsync(client, email);
        if (role != UserRole.User)
        {
            await factory.ChangeRoleAsync(user.Id, role);
        }

        return await AuthApi.LoginAsync(client, email);
    }

    public static Dictionary<string, object?> ValidCreateBody(Guid? ownerId) => new()
    {
        ["owner_id"] = ownerId,
        ["name"] = "Tennis Court 01",
        ["description"] = "Outdoor hard court",
        ["address"] = "1 Tran Phu, Ba Dinh",
        ["city"] = "Ha Noi",
        ["latitude"] = 21.028511m,
        ["longitude"] = 105.804817m,
        ["time_zone_id"] = "Asia/Ho_Chi_Minh"
    };

    public static Dictionary<string, object?> UpdateBodyFrom(CourtDto court, string? status = null) => new()
    {
        ["name"] = court.Name,
        ["description"] = court.Description,
        ["address"] = court.Address,
        ["city"] = court.City,
        ["latitude"] = court.Latitude,
        ["longitude"] = court.Longitude,
        ["time_zone_id"] = court.TimeZoneId,
        ["status"] = status ?? court.Status
    };

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string accessToken, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Post, BasePath, accessToken, body));

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string accessToken, Guid courtId) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Get, PathOf(courtId), accessToken));

    public static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string accessToken, Guid courtId, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Put, PathOf(courtId), accessToken, body));

    public static async Task<CourtDto> CreateCourtAsync(HttpClient client, string adminToken, Guid ownerId)
    {
        using var response = await CreateAsync(client, adminToken, ValidCreateBody(ownerId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadCourtAsync(response);
    }

    public static async Task<CourtDto> ReadCourtAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<CourtDto>(AuthApi.Json))!;

    public sealed record CourtDto(
        Guid Id,
        Guid OwnerId,
        string Name,
        string? Description,
        string Address,
        string City,
        decimal Latitude,
        decimal Longitude,
        string TimeZoneId,
        string Status,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
