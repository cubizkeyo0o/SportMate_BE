using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

/// <summary>
/// Read-only discovery under /api/v1/discovery/courts: any authenticated user sees Active courts only.
/// </summary>
public sealed class CourtDiscoveryTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string ListPath = "/api/v1/discovery/courts";
    private static readonly Guid Pickleball = SportSeed.Sports.Single(s => s.Slug == "pickleball").Id;

    private static readonly string[] CourtFields =
        ["id", "name", "description", "address", "city", "latitude", "longitude", "time_zone_id", "status"];

    private static string PathOf(Guid courtId) => $"{ListPath}/{courtId}";

    // ---- authentication ----

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);

        foreach (var path in new[] { ListPath, PathOf(court.Court.Id), $"{PathOf(court.Court.Id)}/sports", $"{PathOf(court.Court.Id)}/operating-hours" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)] // not the owner of the court
    [InlineData(UserRole.Admin)]
    public async Task EveryRole_CanUseEveryDiscoveryEndpoint(UserRole role)
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var list = await GetAsync(client, caller.AccessToken, ListPath);
        using var detail = await GetAsync(client, caller.AccessToken, PathOf(court.Court.Id));
        using var sports = await GetAsync(client, caller.AccessToken, $"{PathOf(court.Court.Id)}/sports");
        using var hours = await GetAsync(client, caller.AccessToken, $"{PathOf(court.Court.Id)}/operating-hours");

        Assert.All([list, detail, sports, hours], r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Contains((await list.Content.ReadFromJsonAsync<List<CourtDto>>(AuthApi.Json))!, c => c.Id == court.Court.Id);
    }

    // ---- list ----

    [Fact]
    public async Task List_ContainsActiveCourts_AndExcludesInactiveOnes()
    {
        using var client = factory.CreateClient();
        var active = await BookingApi.ArrangeCourtAsync(factory, client);
        var inactive = await BookingApi.ArrangeCourtAsync(factory, client);
        await DeactivateAsync(client, inactive);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, ListPath);

        var courts = (await response.Content.ReadFromJsonAsync<List<CourtDto>>(AuthApi.Json))!;
        Assert.Contains(courts, c => c.Id == active.Court.Id);
        Assert.DoesNotContain(courts, c => c.Id == inactive.Court.Id);
        Assert.All(courts, c => Assert.Equal("active", c.Status));
    }

    [Fact]
    public async Task List_IncludesActiveCourtWithoutSportsOrHours()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var bare = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var list = await GetAsync(client, user.AccessToken, ListPath);
        using var sports = await GetAsync(client, user.AccessToken, $"{PathOf(bare.Id)}/sports");
        using var hours = await GetAsync(client, user.AccessToken, $"{PathOf(bare.Id)}/operating-hours");

        Assert.Contains((await list.Content.ReadFromJsonAsync<List<CourtDto>>(AuthApi.Json))!, c => c.Id == bare.Id);
        Assert.Equal("[]", (await sports.Content.ReadAsStringAsync()).Trim());
        Assert.Equal("[]", (await hours.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        foreach (var name in new[] { "Zeta Arena", "Alpha Park", "Mid Court" })
        {
            var body = CourtApi.ValidCreateBody(owner.User.Id);
            body["name"] = name;
            using var created = await CourtApi.CreateAsync(client, admin.AccessToken, body);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var user = await AuthApi.RegisterAndLoginAsync(client);
        using var response = await GetAsync(client, user.AccessToken, ListPath);

        var names = (await response.Content.ReadFromJsonAsync<List<CourtDto>>(AuthApi.Json))!.Select(c => c.Name).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.True(names.IndexOf("Alpha Park") < names.IndexOf("Mid Court"));
        Assert.True(names.IndexOf("Mid Court") < names.IndexOf("Zeta Arena"));
    }

    [Fact]
    public async Task ListItems_ExposeOnlyUserFacingFields()
    {
        using var client = factory.CreateClient();
        await BookingApi.ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, ListPath);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEmpty(json.RootElement.EnumerateArray());
        Assert.All(json.RootElement.EnumerateArray(), item => AssertExactFields(item, CourtFields));
    }

    // ---- detail ----

    [Fact]
    public async Task Detail_ReturnsTheActiveCourtWithUserFacingFieldsOnly()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, PathOf(court.Court.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        AssertExactFields(json.RootElement, CourtFields);
        Assert.DoesNotContain(court.Owner.User.Id.ToString(), body, StringComparison.OrdinalIgnoreCase);

        var dto = JsonSerializer.Deserialize<CourtDto>(body, AuthApi.Json)!;
        Assert.Equal(
            new CourtDto(court.Court.Id, "Tennis Court 01", "Outdoor hard court", "1 Tran Phu, Ba Dinh", "Ha Noi",
                21.028511m, 105.804817m, BookingApi.VietnamZone, "active"),
            dto);
    }

    [Fact]
    public async Task InactiveOrUnknownCourt_Returns404OnEveryDetailEndpoint()
    {
        using var client = factory.CreateClient();
        var inactive = await BookingApi.ArrangeCourtAsync(factory, client);
        await DeactivateAsync(client, inactive);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        foreach (var courtId in new[] { inactive.Court.Id, Guid.NewGuid() })
        {
            foreach (var path in new[] { PathOf(courtId), $"{PathOf(courtId)}/sports", $"{PathOf(courtId)}/operating-hours" })
            {
                using var response = await GetAsync(client, user.AccessToken, path);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task InactiveCourt_IsHiddenEvenFromItsOwnerAndAdmin()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        await DeactivateAsync(client, court);

        using var byOwner = await GetAsync(client, court.Owner.AccessToken, PathOf(court.Court.Id));
        using var byAdmin = await GetAsync(client, court.Admin.AccessToken, PathOf(court.Court.Id));

        Assert.Equal(HttpStatusCode.NotFound, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byAdmin.StatusCode);
    }

    [Fact]
    public async Task InactiveAndUnknownCourts_GetTheSameResponse()
    {
        using var client = factory.CreateClient();
        var inactive = await BookingApi.ArrangeCourtAsync(factory, client);
        await DeactivateAsync(client, inactive);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var forInactive = await GetAsync(client, user.AccessToken, PathOf(inactive.Court.Id));
        using var forUnknown = await GetAsync(client, user.AccessToken, PathOf(Guid.NewGuid()));

        Assert.Equal(await TitleAsync(forUnknown), await TitleAsync(forInactive));
    }

    // ---- sports ----

    [Fact]
    public async Task Sports_ReturnOnlyActiveLinksOfActiveSports_OrderedBySportName()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client); // Tennis 100,000 and Badminton 80,000
        var inactiveSport = await factory.AddSportAsync(name: $"Aaa Inactive {Guid.NewGuid():N}"[..30]);
        var inactiveLink = new CourtSport(court.Court.Id, Pickleball, 50_000m, "VND", DateTime.UtcNow);
        inactiveLink.SetActive(false, DateTime.UtcNow);
        await factory.WithDbAsync(async db =>
        {
            db.CourtSports.Add(new CourtSport(court.Court.Id, inactiveSport.Id, 10_000m, "VND", DateTime.UtcNow));
            db.CourtSports.Add(inactiveLink);
            await db.SaveChangesAsync();
        });
        await factory.SetSportActiveAsync(inactiveSport.Id, false);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, $"{PathOf(court.Court.Id)}/sports");

        var sports = (await response.Content.ReadFromJsonAsync<List<CourtSportDto>>(AuthApi.Json))!;
        Assert.Equal(
            [
                new CourtSportDto(new SportDto(BookingApi.Badminton, "Badminton", "badminton"), 80_000m, "VND", true),
                new CourtSportDto(new SportDto(BookingApi.Tennis, "Tennis", "tennis"), 100_000m, "VND", true)
            ],
            sports);
    }

    [Fact]
    public async Task Sports_ExposeOnlyUserFacingFields_WithPriceAndUppercaseCurrency()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        using (await client.SendAsync(AuthApi.Authorized(HttpMethod.Put, $"/api/v1/courts/{court.Court.Id}/sports/{BookingApi.Tennis}",
                   court.Admin.AccessToken, new { price_per_hour = 125_000.50m, currency = "usd", is_active = true })))
        {
        }

        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, $"{PathOf(court.Court.Id)}/sports");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.EnumerateArray().ToList();
        Assert.All(items, item =>
        {
            AssertExactFields(item, ["sport", "price_per_hour", "currency", "is_active"]);
            AssertExactFields(item.GetProperty("sport"), ["id", "name", "slug"]);
        });
        var tennis = items.Single(i => i.GetProperty("sport").GetProperty("slug").GetString() == "tennis");
        Assert.Equal(JsonValueKind.Number, tennis.GetProperty("price_per_hour").ValueKind);
        Assert.Equal(125_000.50m, tennis.GetProperty("price_per_hour").GetDecimal());
        Assert.Equal("USD", tennis.GetProperty("currency").GetString());
        Assert.True(tennis.GetProperty("is_active").GetBoolean());
    }

    // ---- operating hours ----

    [Fact]
    public async Task OperatingHours_ReturnLocalTimesOrderedByDayThenOpening_WithoutManagementFields()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(
            factory, client,
            periods: [("14:00", "23:00"), ("06:00", "12:00"), ("12:00", "14:00")],
            days: [DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, $"{PathOf(court.Court.Id)}/operating-hours");

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.All(json.RootElement.EnumerateArray(), item => AssertExactFields(item, ["day_of_week", "opens_at", "closes_at"]));

        var hours = JsonSerializer.Deserialize<List<HourDto>>(body, AuthApi.Json)!;
        Assert.Equal(
            [
                new HourDto(0, "06:00", "12:00"), new HourDto(0, "12:00", "14:00"), new HourDto(0, "14:00", "23:00"),
                new HourDto(1, "06:00", "12:00"), new HourDto(1, "12:00", "14:00"), new HourDto(1, "14:00", "23:00"),
                new HourDto(6, "06:00", "12:00"), new HourDto(6, "12:00", "14:00"), new HourDto(6, "14:00", "23:00")
            ],
            hours);
    }

    [Fact]
    public async Task OperatingHours_AreNotConvertedToUtc()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client, "America/New_York",
            periods: [("08:30", "21:45")], days: [DayOfWeek.Wednesday]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await GetAsync(client, user.AccessToken, $"{PathOf(court.Court.Id)}/operating-hours");

        Assert.Equal([new HourDto(3, "08:30", "21:45")], (await response.Content.ReadFromJsonAsync<List<HourDto>>(AuthApi.Json))!);
    }

    // ---- management contracts are unchanged ----

    [Fact]
    public async Task ManagementReads_KeepTheirPermissionsAndShapes()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var managementPaths = new[]
        {
            $"/api/v1/courts/{court.Court.Id}", $"/api/v1/courts/{court.Court.Id}/sports", $"/api/v1/courts/{court.Court.Id}/operating-hours"
        };

        foreach (var path in managementPaths)
        {
            using var byUser = await GetAsync(client, user.AccessToken, path);
            using var byOtherOwner = await GetAsync(client, otherOwner.AccessToken, path);
            Assert.Equal(HttpStatusCode.Forbidden, byUser.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, byOtherOwner.StatusCode);
        }

        using var courtResponse = await GetAsync(client, court.Owner.AccessToken, managementPaths[0]);
        using var sportsResponse = await GetAsync(client, court.Owner.AccessToken, managementPaths[1]);
        using var hoursResponse = await GetAsync(client, court.Owner.AccessToken, managementPaths[2]);
        using var courtJson = JsonDocument.Parse(await courtResponse.Content.ReadAsStringAsync());
        using var sportsJson = JsonDocument.Parse(await sportsResponse.Content.ReadAsStringAsync());
        using var hoursJson = JsonDocument.Parse(await hoursResponse.Content.ReadAsStringAsync());

        AssertExactFields(courtJson.RootElement,
            ["id", "owner_id", "name", "description", "address", "city", "latitude", "longitude", "time_zone_id", "status", "created_at_utc", "updated_at_utc"]);
        AssertExactFields(sportsJson.RootElement[0],
            ["sport", "price_per_hour", "currency", "is_active", "created_at_utc", "updated_at_utc"]);
        AssertExactFields(sportsJson.RootElement[0].GetProperty("sport"), ["id", "name", "slug", "is_active"]);
        AssertExactFields(hoursJson.RootElement[0],
            ["id", "day_of_week", "opens_at", "closes_at", "created_at_utc", "updated_at_utc"]);
    }

    [Fact]
    public async Task DiscoveryIsReadOnly_UsersStillCannotManageCourts()
    {
        using var client = factory.CreateClient();
        var court = await BookingApi.ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var put = await CourtApi.UpdateAsync(client, user.AccessToken, court.Court.Id, CourtApi.UpdateBodyFrom(court.Court, "inactive"));
        using var addSport = await client.SendAsync(AuthApi.Authorized(HttpMethod.Post, $"/api/v1/courts/{court.Court.Id}/sports",
            user.AccessToken, new { sport_id = Pickleball, price_per_hour = 1m, currency = "VND" }));
        using var postToDiscovery = await client.SendAsync(AuthApi.Authorized(HttpMethod.Post, ListPath, user.AccessToken, new { name = "x" }));

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, addSport.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postToDiscovery.StatusCode);
    }

    // ---- helpers ----

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string path) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Get, path, token));

    private static async Task DeactivateAsync(HttpClient client, BookingApi.ArrangedCourt court)
    {
        using var response = await CourtApi.UpdateAsync(
            client, court.Admin.AccessToken, court.Court.Id, CourtApi.UpdateBodyFrom(court.Court, "inactive"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static void AssertExactFields(JsonElement element, string[] expected) =>
        Assert.Equal(expected.Order(), element.EnumerateObject().Select(p => p.Name).Order());

    private static async Task<string?> TitleAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("title").GetString();
    }

    private sealed record CourtDto(
        Guid Id, string Name, string? Description, string Address, string City,
        decimal Latitude, decimal Longitude, string TimeZoneId, string Status);

    private sealed record SportDto(Guid Id, string Name, string Slug);

    private sealed record CourtSportDto(SportDto Sport, decimal PricePerHour, string Currency, bool IsActive);

    private sealed record HourDto(int DayOfWeek, string OpensAt, string ClosesAt);
}

/// <summary>
/// Its own factory, so the database has no courts at all.
/// </summary>
public sealed class CourtDiscoveryEmptyTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task List_WithNoActiveCourts_ReturnsEmptyArray()
    {
        using var client = factory.CreateClient();
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, "/api/v1/discovery/courts", user.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync()).Trim());
    }
}
