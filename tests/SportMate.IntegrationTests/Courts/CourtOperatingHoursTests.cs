using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

public sealed class CourtOperatingHoursTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private static string ListPath(Guid courtId) => $"/api/v1/courts/{courtId}/operating-hours";

    private static string ItemPath(Guid courtId, Guid id) => $"/api/v1/courts/{courtId}/operating-hours/{id}";

    private static object Period(object? day, string? opensAt, string? closesAt) =>
        new { day_of_week = day, opens_at = opensAt, closes_at = closesAt };

    // ---- authorization ----

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var hour = await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));

        using var list = await client.GetAsync(ListPath(court.Id));
        using var add = await client.PostAsJsonAsync(ListPath(court.Id), Period(2, "06:00", "12:00"), AuthApi.Json);
        using var update = await client.PutAsJsonAsync(ItemPath(court.Id, hour.Id), Period(1, "07:00", "12:00"), AuthApi.Json);
        using var delete = await client.DeleteAsync(ItemPath(court.Id, hour.Id));

        Assert.All([list, add, update, delete], r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)] // a CourtOwner who does not own this court
    public async Task Endpoints_ForNonManagers_Return403AndChangeNothing(UserRole role)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var hour = await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var list = await Send(client, HttpMethod.Get, ListPath(court.Id), caller.AccessToken);
        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), caller.AccessToken, Period(2, "06:00", "12:00"));
        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, hour.Id), caller.AccessToken, Period(1, "07:00", "12:00"));
        using var delete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, hour.Id), caller.AccessToken);

        Assert.All([list, add, update, delete], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        var stored = Assert.Single(await StoredAsync(court.Id));
        Assert.Equal(new TimeOnly(6, 0), stored.OpensAt);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.CourtOwner)] // the assigned owner
    public async Task Managers_CanListAddUpdateAndDelete(UserRole role)
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var token = role == UserRole.Admin ? admin.AccessToken : owner.AccessToken;

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), token, Period(1, "06:00", "12:00"));
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var created = await ReadAsync(add);
        Assert.Equal(ItemPath(court.Id, created.Id), add.Headers.Location?.OriginalString);

        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, created.Id), token, Period(3, "07:30", "11:45"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync(update);
        Assert.Equal((created.Id, 3, "07:30", "11:45"), (updated.Id, updated.DayOfWeek, updated.OpensAt, updated.ClosesAt));

        Assert.Equal([updated], await ListAsync(client, token, court.Id));

        using var delete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, created.Id), token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await StoredAsync(court.Id));
    }

    [Fact]
    public async Task DemotedOwner_ImmediatelyGets403()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var hour = await AddAsync(client, owner.AccessToken, court.Id, Period(1, "06:00", "12:00"));

        await factory.ChangeRoleAsync(owner.User.Id, UserRole.User);

        using var list = await Send(client, HttpMethod.Get, ListPath(court.Id), owner.AccessToken);
        using var delete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, hour.Id), owner.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Single(await StoredAsync(court.Id));
    }

    [Fact]
    public async Task SuspendedOwner_IsRejectedWith401()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);

        await factory.SuspendAsync(owner.User.Id);

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), owner.AccessToken, Period(1, "06:00", "12:00"));
        Assert.Equal(HttpStatusCode.Unauthorized, add.StatusCode);
        Assert.Empty(await StoredAsync(court.Id));
    }

    [Fact]
    public async Task OwnershipCannotBeSpoofed_AndPeriodsStayOnTheirOwnCourt()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var otherCourt = await CourtApi.CreateCourtAsync(client, admin.AccessToken, otherOwner.User.Id);
        var otherHour = await AddAsync(client, otherOwner.AccessToken, otherCourt.Id, Period(1, "06:00", "12:00"));

        // Another owner naming themselves or their court in the body is still refused.
        using var spoof = await Send(client, HttpMethod.Post, ListPath(court.Id), otherOwner.AccessToken, new
        {
            day_of_week = 1, opens_at = "06:00", closes_at = "12:00", owner_id = otherOwner.User.Id, court_id = otherCourt.Id
        });
        Assert.Equal(HttpStatusCode.Forbidden, spoof.StatusCode);

        // The real owner cannot reach another court's period through their own court's path.
        using var crossUpdate = await Send(client, HttpMethod.Put, ItemPath(court.Id, otherHour.Id), owner.AccessToken, Period(1, "08:00", "09:00"));
        using var crossDelete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, otherHour.Id), owner.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, crossUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossDelete.StatusCode);

        Assert.Empty(await StoredAsync(court.Id));
        var untouched = Assert.Single(await StoredAsync(otherCourt.Id));
        Assert.Equal(new TimeOnly(6, 0), untouched.OpensAt);
    }

    // ---- not found ----

    [Fact]
    public async Task UnknownCourtOrPeriod_Returns404()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var missingCourt = Guid.NewGuid();

        using var list = await Send(client, HttpMethod.Get, ListPath(missingCourt), admin.AccessToken);
        using var add = await Send(client, HttpMethod.Post, ListPath(missingCourt), admin.AccessToken, Period(1, "06:00", "12:00"));
        using var updateMissing = await Send(client, HttpMethod.Put, ItemPath(court.Id, Guid.NewGuid()), admin.AccessToken, Period(1, "06:00", "12:00"));
        using var deleteMissing = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Guid.NewGuid()), admin.AccessToken);

        Assert.All([list, add, updateMissing, deleteMissing], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    // ---- validation ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Add_AcceptsEveryDayOfWeek(int day)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        var hour = await AddAsync(client, admin.AccessToken, court.Id, Period(day, "06:00", "12:00"));

        Assert.Equal(day, hour.DayOfWeek);
        Assert.Equal((DayOfWeek)day, Assert.Single(await StoredAsync(court.Id)).DayOfWeek);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(null)]
    public async Task Add_WithInvalidDay_Returns400(int? day)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, Period(day, "06:00", "12:00"));

        await CreateCourtTests.AssertValidationErrorAsync(response, "day_of_week");
        Assert.Empty(await StoredAsync(court.Id));
    }

    [Theory]
    [InlineData("12:00", "12:00", "closes_at")] // equal
    [InlineData("14:00", "12:00", "closes_at")] // reversed
    [InlineData("22:00", "02:00", "closes_at")] // crosses midnight
    [InlineData("06:00", "24:00", "closes_at")] // 24:00 not supported
    [InlineData("06:00:00", "12:00", "opens_at")] // seconds
    [InlineData("06:00", "12:00:30", "closes_at")]
    [InlineData("6", "12:00", "opens_at")]
    [InlineData(null, "12:00", "opens_at")]
    [InlineData("06:00", null, "closes_at")]
    public async Task Add_WithInvalidTimes_Returns400(string? opensAt, string? closesAt, string field)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, Period(1, opensAt, closesAt));

        await CreateCourtTests.AssertValidationErrorAsync(response, field);
        Assert.Empty(await StoredAsync(court.Id));
    }

    [Fact]
    public async Task Add_AllowsSeveralNonOverlappingAndTouchingPeriodsOnOneDay()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "14:00", "23:00"));
        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));
        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "12:00", "14:00")); // touches both
        await AddAsync(client, admin.AccessToken, court.Id, Period(0, "06:00", "12:00")); // same times, other day

        var hours = await ListAsync(client, admin.AccessToken, court.Id);
        Assert.Equal(
            [(0, "06:00", "12:00"), (1, "06:00", "12:00"), (1, "12:00", "14:00"), (1, "14:00", "23:00")],
            hours.Select(h => (h.DayOfWeek, h.OpensAt, h.ClosesAt)));
    }

    [Theory]
    [InlineData("06:00", "12:00")] // identical (same opening time)
    [InlineData("06:00", "08:00")] // same opening time
    [InlineData("11:00", "13:00")] // overlaps the end
    [InlineData("05:00", "07:00")] // overlaps the start
    [InlineData("08:00", "09:00")] // inside
    [InlineData("05:00", "13:00")] // contains
    public async Task Add_OverlappingPeriodOnSameDay_Returns400(string opensAt, string closesAt)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, Period(1, opensAt, closesAt));

        await CreateCourtTests.AssertValidationErrorAsync(response, "opens_at");
        Assert.Single(await StoredAsync(court.Id));
    }

    [Fact]
    public async Task Overlap_IsCheckedOnlyWithinTheSameCourt()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var secondCourt = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);
        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));

        var onSecondCourt = await AddAsync(client, admin.AccessToken, secondCourt.Id, Period(1, "06:00", "12:00"));

        Assert.Equal("06:00", onSecondCourt.OpensAt);
    }

    [Fact]
    public async Task Update_RejectsOverlapWithAnotherPeriod_ButMayOverlapItsOwnOldTimes()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var morning = await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));
        await AddAsync(client, admin.AccessToken, court.Id, Period(1, "14:00", "23:00"));

        using var extendOwn = await Send(client, HttpMethod.Put, ItemPath(court.Id, morning.Id), admin.AccessToken, Period(1, "05:00", "14:00"));
        Assert.Equal(HttpStatusCode.OK, extendOwn.StatusCode); // touches 14:00, overlaps only its old self

        using var overlap = await Send(client, HttpMethod.Put, ItemPath(court.Id, morning.Id), admin.AccessToken, Period(1, "05:00", "15:00"));
        await CreateCourtTests.AssertValidationErrorAsync(overlap, "opens_at");

        using var invalid = await Send(client, HttpMethod.Put, ItemPath(court.Id, morning.Id), admin.AccessToken, Period(1, "10:00", "09:00"));
        await CreateCourtTests.AssertValidationErrorAsync(invalid, "closes_at");

        var stored = (await StoredAsync(court.Id)).Single(h => h.Id == morning.Id);
        Assert.Equal((new TimeOnly(5, 0), new TimeOnly(14, 0)), (stored.OpensAt, stored.ClosesAt));
    }

    [Fact]
    public async Task Update_CanMovePeriodToAnotherDay()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var monday = await AddAsync(client, admin.AccessToken, court.Id, Period(1, "06:00", "12:00"));
        await AddAsync(client, admin.AccessToken, court.Id, Period(2, "06:00", "12:00"));

        using var clash = await Send(client, HttpMethod.Put, ItemPath(court.Id, monday.Id), admin.AccessToken, Period(2, "11:00", "13:00"));
        await CreateCourtTests.AssertValidationErrorAsync(clash, "opens_at");

        using var moved = await Send(client, HttpMethod.Put, ItemPath(court.Id, monday.Id), admin.AccessToken, Period(2, "12:00", "13:00"));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(2, (await ReadAsync(moved)).DayOfWeek);
    }

    // ---- timestamps ----

    [Fact]
    public async Task Timestamps_AreServerControlled()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var before = DateTime.UtcNow;

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, new
        {
            day_of_week = 1, opens_at = "06:00", closes_at = "12:00",
            id = Guid.NewGuid(), created_at_utc = "2000-01-01T00:00:00Z", updated_at_utc = "2000-01-01T00:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var created = await ReadAsync(add);
        Assert.InRange(created.CreatedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(created.CreatedAtUtc, created.UpdatedAtUtc);

        using var sameValues = await Send(client, HttpMethod.Put, ItemPath(court.Id, created.Id), admin.AccessToken, new
        {
            day_of_week = 1, opens_at = "06:00", closes_at = "12:00", updated_at_utc = "2000-01-01T00:00:00Z"
        });
        Assert.Equal(created, await ReadAsync(sameValues)); // no-op keeps updated_at_utc

        using var changed = await Send(client, HttpMethod.Put, ItemPath(court.Id, created.Id), admin.AccessToken, new
        {
            day_of_week = 1, opens_at = "07:00", closes_at = "12:00", created_at_utc = "2000-01-01T00:00:00Z"
        });
        var updated = await ReadAsync(changed);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > created.UpdatedAtUtc);

        var stored = Assert.Single(await StoredAsync(court.Id));
        Assert.Equal(created.CreatedAtUtc, stored.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedAtUtc.Kind);
    }

    // ---- database backstop ----

    [Fact]
    public async Task UniqueIndex_RejectsSameOpeningTimeOnSameDay()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);

        await factory.WithDbAsync(async db =>
        {
            db.CourtOperatingHours.Add(new CourtOperatingHour(court.Id, DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(8, 0), DateTime.UtcNow));
            db.CourtOperatingHours.Add(new CourtOperatingHour(court.Id, DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(9, 0), DateTime.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    // ---- helpers ----

    private async Task<(AuthApi.AuthDto Admin, AuthApi.AuthDto Owner, CourtApi.CourtDto Court)> ArrangeCourtAsync(
        HttpClient client)
    {
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var court = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);
        return (admin, owner, court);
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client, HttpMethod method, string path, string token, object? body = null) =>
        client.SendAsync(AuthApi.Authorized(method, path, token, body));

    private static async Task<HourDto> AddAsync(HttpClient client, string token, Guid courtId, object body)
    {
        using var response = await Send(client, HttpMethod.Post, ListPath(courtId), token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<List<HourDto>> ListAsync(HttpClient client, string token, Guid courtId)
    {
        using var response = await Send(client, HttpMethod.Get, ListPath(courtId), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<HourDto>>(AuthApi.Json))!;
    }

    private static async Task<HourDto> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<HourDto>(AuthApi.Json))!;

    private async Task<List<CourtOperatingHour>> StoredAsync(Guid courtId)
    {
        List<CourtOperatingHour> hours = [];
        await factory.WithDbAsync(async db => hours = await db.CourtOperatingHours.AsNoTracking()
            .Where(h => h.CourtId == courtId).ToListAsync());
        return hours;
    }

    private sealed record HourDto(
        Guid Id,
        int DayOfWeek,
        string OpensAt,
        string ClosesAt,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
