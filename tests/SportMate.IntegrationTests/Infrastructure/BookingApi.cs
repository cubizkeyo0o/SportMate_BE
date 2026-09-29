using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// Helpers for booking tests: a court with two sports and weekly operating hours, local timestamps, and the
/// booking endpoints.
/// </summary>
public static class BookingApi
{
    public const string BasePath = "/api/v1/bookings";
    public const string VietnamZone = "Asia/Ho_Chi_Minh"; // UTC+07:00, no daylight saving
    public const decimal TennisPrice = 100_000m;
    public const decimal BadmintonPrice = 80_000m;

    public static readonly Guid Tennis = SportSeed.Sports.Single(s => s.Slug == "tennis").Id;
    public static readonly Guid Badminton = SportSeed.Sports.Single(s => s.Slug == "badminton").Id;

    /// <summary>
    /// Default weekly hours for every day: 06:00-12:00 and 12:00-14:00 touch; 14:00-15:00 is a gap.
    /// </summary>
    public static readonly (string Opens, string Closes)[] DefaultPeriods =
        [("06:00", "12:00"), ("12:00", "14:00"), ("15:00", "22:00")];

    public static string PathOf(Guid bookingId) => $"{BasePath}/{bookingId}";

    public static async Task<ArrangedCourt> ArrangeCourtAsync(
        SportMateApiFactory factory,
        HttpClient client,
        string timeZoneId = VietnamZone,
        IReadOnlyList<(string Opens, string Closes)>? periods = null,
        IReadOnlyList<DayOfWeek>? days = null)
    {
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.ValidCreateBody(owner.User.Id);
        body["time_zone_id"] = timeZoneId;
        using var created = await CourtApi.CreateAsync(client, admin.AccessToken, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var court = await CourtApi.ReadCourtAsync(created);

        var now = DateTime.UtcNow;
        var tennis = new CourtSport(court.Id, Tennis, TennisPrice, "VND", now);
        var badminton = new CourtSport(court.Id, Badminton, BadmintonPrice, "VND", now);
        await factory.WithDbAsync(async db =>
        {
            db.CourtSports.AddRange(tennis, badminton);
            foreach (var day in days ?? Enum.GetValues<DayOfWeek>())
            {
                foreach (var (opens, closes) in periods ?? DefaultPeriods)
                {
                    db.CourtOperatingHours.Add(new CourtOperatingHour(
                        court.Id, day, TimeOnly.Parse(opens, CultureInfo.InvariantCulture),
                        TimeOnly.Parse(closes, CultureInfo.InvariantCulture), now));
                }
            }

            await db.SaveChangesAsync();
        });

        return new ArrangedCourt(admin, owner, court, tennis.Id, badminton.Id);
    }

    /// <summary>
    /// A local calendar date of the zone, <paramref name="daysAhead"/> days after today there.
    /// </summary>
    public static DateOnly LocalDate(string timeZoneId = VietnamZone, int daysAhead = 7)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).AddDays(daysAhead);
    }

    /// <summary>
    /// ISO-8601 timestamp for a Vietnam-local wall-clock time, with the explicit +07:00 offset.
    /// </summary>
    public static string VietnamTime(DateOnly date, string time) =>
        $"{date:yyyy-MM-dd}T{time}:00+07:00";

    public static object Body(Guid courtSportId, string startsAt, string endsAt) =>
        new { court_sport_id = courtSportId, starts_at = startsAt, ends_at = endsAt };

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Post, BasePath, token, body));

    public static async Task<BookingDto> CreateBookingAsync(HttpClient client, string token, object body)
    {
        using var response = await CreateAsync(client, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    public static Task<HttpResponseMessage> ActionAsync(
        HttpClient client, string token, Guid bookingId, string action, object? body = null) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Post, $"{PathOf(bookingId)}/{action}", token, body));

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string path) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Get, path, token));

    public static async Task<BookingDto> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<BookingDto>(AuthApi.Json))!;

    public static async Task<List<BookingDto>> ReadListAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<BookingDto>>(AuthApi.Json))!;
    }

    public sealed record ArrangedCourt(
        AuthApi.AuthDto Admin,
        AuthApi.AuthDto Owner,
        CourtApi.CourtDto Court,
        Guid TennisCourtSportId,
        Guid BadmintonCourtSportId);

    public sealed record SportDto(Guid Id, string Name, string Slug, bool IsActive);

    public sealed record BookingDto(
        Guid Id,
        Guid UserId,
        Guid CourtId,
        Guid CourtSportId,
        SportDto Sport,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        string TimeZoneId,
        string LocalDate,
        string LocalStartTime,
        string LocalEndTime,
        string Status,
        decimal TotalPrice,
        string Currency,
        DateTime? HoldExpiresAtUtc,
        string? CancellationReason,
        DateTime? CancelledAtUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
