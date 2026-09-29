using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

public sealed class CourtSportsTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private static readonly Guid Tennis = SportSeed.Sports.Single(s => s.Slug == "tennis").Id;
    private static readonly Guid Pickleball = SportSeed.Sports.Single(s => s.Slug == "pickleball").Id;
    private static readonly Guid Badminton = SportSeed.Sports.Single(s => s.Slug == "badminton").Id;

    private static string ListPath(Guid courtId) => $"/api/v1/courts/{courtId}/sports";

    private static string ItemPath(Guid courtId, Guid sportId) => $"/api/v1/courts/{courtId}/sports/{sportId}";

    private static object AddBody(Guid sportId, decimal price = 150_000m, string currency = "VND") =>
        new { sport_id = sportId, price_per_hour = price, currency };

    private static object UpdateBody(decimal price = 150_000m, string currency = "VND", bool isActive = true) =>
        new { price_per_hour = price, currency, is_active = isActive };

    // ---- authorization ----

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);

        using var list = await client.GetAsync(ListPath(court.Id));
        using var add = await client.PostAsJsonAsync(ListPath(court.Id), AddBody(Tennis), AuthApi.Json);
        using var update = await client.PutAsJsonAsync(ItemPath(court.Id, Tennis), UpdateBody(), AuthApi.Json);
        using var delete = await client.DeleteAsync(ItemPath(court.Id, Tennis));

        Assert.All([list, add, update, delete], r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)] // a CourtOwner who does not own this court
    public async Task Endpoints_ForNonManagers_Return403AndChangeNothing(UserRole role)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var list = await Send(client, HttpMethod.Get, ListPath(court.Id), caller.AccessToken);
        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), caller.AccessToken, AddBody(Pickleball));
        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), caller.AccessToken, UpdateBody(1m));
        using var delete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Tennis), caller.AccessToken);

        Assert.All([list, add, update, delete], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.CourtSports.AsNoTracking().SingleAsync(cs => cs.CourtId == court.Id);
            Assert.Equal(Tennis, stored.SportId);
            Assert.Equal(150_000m, stored.PricePerHour);
            Assert.True(stored.IsActive);
        });
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.CourtOwner)] // the assigned owner
    public async Task Managers_CanListAddUpdateAndDeactivate(UserRole role)
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var token = role == UserRole.Admin ? admin.AccessToken : owner.AccessToken;

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), token, AddBody(Tennis));
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(ItemPath(court.Id, Tennis), add.Headers.Location?.OriginalString);

        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), token, UpdateBody(200_000m));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(200_000m, (await ReadAsync(update)).PricePerHour);

        using var delete = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Tennis), token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listed = Assert.Single(await ListAsync(client, token, court.Id));
        Assert.False(listed.IsActive);
        Assert.Equal(200_000m, listed.PricePerHour);
    }

    [Fact]
    public async Task UnrelatedAdmin_ManagesAnyCourt()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);
        var otherAdmin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), otherAdmin.AccessToken, AddBody(Tennis));

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
    }

    [Fact]
    public async Task DemotedOwner_ImmediatelyGets403()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, owner.AccessToken, court.Id, AddBody(Tennis));

        await factory.ChangeRoleAsync(owner.User.Id, UserRole.User);

        using var list = await Send(client, HttpMethod.Get, ListPath(court.Id), owner.AccessToken);
        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), owner.AccessToken, UpdateBody(1m));
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task SuspendedOwner_IsRejectedWith401()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);

        await factory.SuspendAsync(owner.User.Id);

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), owner.AccessToken, AddBody(Tennis));
        Assert.Equal(HttpStatusCode.Unauthorized, add.StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.CourtSports.AnyAsync(cs => cs.CourtId == court.Id)));
    }

    [Fact]
    public async Task OwnershipCannotBeSpoofedThroughTheBody()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var otherCourt = await CourtApi.CreateCourtAsync(client, admin.AccessToken, otherOwner.User.Id);

        // Another owner claiming ownership or naming their own court in the body is still refused.
        using var spoof = await Send(client, HttpMethod.Post, ListPath(court.Id), otherOwner.AccessToken, new
        {
            sport_id = Tennis, price_per_hour = 1m, currency = "VND",
            owner_id = otherOwner.User.Id, court_id = otherCourt.Id
        });
        Assert.Equal(HttpStatusCode.Forbidden, spoof.StatusCode);

        // The real owner naming another court in the body only affects the court in the path.
        using var own = await Send(client, HttpMethod.Post, ListPath(court.Id), owner.AccessToken, new
        {
            sport_id = Tennis, price_per_hour = 1m, currency = "VND", court_id = otherCourt.Id
        });
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            Assert.True(await db.CourtSports.AnyAsync(cs => cs.CourtId == court.Id && cs.SportId == Tennis));
            Assert.False(await db.CourtSports.AnyAsync(cs => cs.CourtId == otherCourt.Id));
        });
    }

    // ---- not found ----

    [Fact]
    public async Task UnknownCourt_Returns404()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var missing = Guid.NewGuid();

        using var list = await Send(client, HttpMethod.Get, ListPath(missing), admin.AccessToken);
        using var add = await Send(client, HttpMethod.Post, ListPath(missing), admin.AccessToken, AddBody(Tennis));
        using var update = await Send(client, HttpMethod.Put, ItemPath(missing, Tennis), admin.AccessToken, UpdateBody());
        using var delete = await Send(client, HttpMethod.Delete, ItemPath(missing, Tennis), admin.AccessToken);

        Assert.All([list, add, update, delete], r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task UnknownSport_Returns404()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var add = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(Guid.NewGuid()));
        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, Guid.NewGuid()), admin.AccessToken, UpdateBody());

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task UpdatingASportTheCourtDoesNotHave_Returns404()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var update = await Send(client, HttpMethod.Put, ItemPath(court.Id, Badminton), admin.AccessToken, UpdateBody());

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    // ---- add ----

    [Fact]
    public async Task Add_PersistsActiveRelationshipWithServerTimestamps()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var before = DateTime.UtcNow;

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, new
        {
            sport_id = Tennis,
            price_per_hour = 150_000.50m,
            currency = "vnd",
            is_active = false,
            created_at_utc = "2000-01-01T00:00:00Z",
            updated_at_utc = "2000-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(new SportDto(Tennis, "Tennis", "tennis", true), body.Sport);
        Assert.Equal(150_000.50m, body.PricePerHour);
        Assert.Equal("VND", body.Currency);
        Assert.True(body.IsActive);
        Assert.InRange(body.CreatedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(body.CreatedAtUtc, body.UpdatedAtUtc);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.CourtSports.AsNoTracking().SingleAsync(cs => cs.CourtId == court.Id);
            Assert.Equal(150_000.50m, stored.PricePerHour);
            Assert.Equal("VND", stored.Currency);
            Assert.True(stored.IsActive);
            Assert.InRange(stored.CreatedAtUtc, before, DateTime.UtcNow);
        });
    }

    [Fact]
    public async Task Add_AcceptsZeroPrice()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(Tennis, 0m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0m, (await ReadAsync(response)).PricePerHour);
    }

    [Theory]
    [InlineData(-1, "VND", "price_per_hour")]
    [InlineData(10.123, "VND", "price_per_hour")]
    [InlineData(100000000, "VND", "price_per_hour")]
    [InlineData(1, "VN", "currency")]
    [InlineData(1, "VNDX", "currency")]
    [InlineData(1, "12A", "currency")]
    [InlineData(1, "", "currency")]
    public async Task Add_WithInvalidPriceOrCurrency_Returns400(double price, string currency, string field)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var response = await Send(
            client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(Tennis, (decimal)price, currency));

        await CreateCourtTests.AssertValidationErrorAsync(response, field);
        await factory.WithDbAsync(async db => Assert.False(await db.CourtSports.AnyAsync(cs => cs.CourtId == court.Id)));
    }

    [Fact]
    public async Task Add_WithoutRequiredFields_Returns400()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.Equal(["currency", "price_per_hour", "sport_id"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task Add_InactiveSport_Returns400()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var inactive = await factory.AddSportAsync(isActive: false);

        using var response = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(inactive.Id));

        await CreateCourtTests.AssertValidationErrorAsync(response, "sport_id");
        await factory.WithDbAsync(async db => Assert.False(await db.CourtSports.AnyAsync(cs => cs.CourtId == court.Id)));
    }

    [Fact]
    public async Task Add_Duplicate_Returns409_EvenWhenExistingIsInactive()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));

        using var duplicate = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(Tennis, 1m));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using (await Send(client, HttpMethod.Delete, ItemPath(court.Id, Tennis), admin.AccessToken))
        {
        }

        using var afterDeactivate = await Send(client, HttpMethod.Post, ListPath(court.Id), admin.AccessToken, AddBody(Tennis));
        Assert.Equal(HttpStatusCode.Conflict, afterDeactivate.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal(1, await db.CourtSports.CountAsync(cs => cs.CourtId == court.Id)));
    }

    [Fact]
    public async Task Court_CanOfferSeveralSports_AndTheSameSportCanBeOnSeveralCourts()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var secondCourt = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);

        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Pickleball));
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Badminton));
        await AddAsync(client, admin.AccessToken, secondCourt.Id, AddBody(Tennis));

        var sports = await ListAsync(client, admin.AccessToken, court.Id);
        Assert.Equal(["Badminton", "Pickleball", "Tennis"], sports.Select(s => s.Sport.Name));
        Assert.All(sports, s => Assert.True(s.IsActive));
        Assert.Single(await ListAsync(client, admin.AccessToken, secondCourt.Id));
    }

    // ---- update ----

    [Fact]
    public async Task Update_ChangesPriceAndCurrency_KeepingCreatedAt()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var created = await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));

        using var response = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), owner.AccessToken, new
        {
            price_per_hour = 0m,
            currency = "usd",
            is_active = true,
            created_at_utc = "2000-01-01T00:00:00Z",
            updated_at_utc = "2000-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync(response);
        Assert.Equal(0m, updated.PricePerHour);
        Assert.Equal("USD", updated.Currency);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > created.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_WithSameValues_IsANoOp()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var created = await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));

        using var response = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), admin.AccessToken, UpdateBody());

        Assert.Equal(created, await ReadAsync(response));
    }

    [Theory]
    [InlineData("price_per_hour")]
    [InlineData("currency")]
    [InlineData("is_active")]
    public async Task Update_WithMissingField_Returns400(string missing)
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));
        var body = new Dictionary<string, object?> { ["price_per_hour"] = 1m, ["currency"] = "VND", ["is_active"] = true };
        body.Remove(missing);

        using var response = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), admin.AccessToken, body);

        await CreateCourtTests.AssertValidationErrorAsync(response, missing);
    }

    [Fact]
    public async Task Update_NegativePrice_Returns400()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));

        using var response = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), admin.AccessToken, UpdateBody(-5m));

        await CreateCourtTests.AssertValidationErrorAsync(response, "price_per_hour");
    }

    [Fact]
    public async Task Update_CanReactivateWhenSportIsActive()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));
        using (await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), admin.AccessToken, UpdateBody(isActive: false)))
        {
        }

        using var response = await Send(client, HttpMethod.Put, ItemPath(court.Id, Tennis), admin.AccessToken, UpdateBody(isActive: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ReadAsync(response)).IsActive);
    }

    [Fact]
    public async Task Update_CannotReactivateWhileSportIsInactive_ButPriceAndDeactivationStillWork()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);
        var sport = await factory.AddSportAsync();
        await AddAsync(client, admin.AccessToken, court.Id, AddBody(sport.Id));
        await factory.SetSportActiveAsync(sport.Id, false);

        // The relationship stays valid and active after the sport is deactivated; its price can change.
        using var priceChange = await Send(client, HttpMethod.Put, ItemPath(court.Id, sport.Id), admin.AccessToken, UpdateBody(90_000m));
        Assert.Equal(HttpStatusCode.OK, priceChange.StatusCode);
        var afterPrice = await ReadAsync(priceChange);
        Assert.True(afterPrice.IsActive);
        Assert.False(afterPrice.Sport.IsActive);

        using var deactivate = await Send(client, HttpMethod.Put, ItemPath(court.Id, sport.Id), admin.AccessToken, UpdateBody(90_000m, isActive: false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.False((await ReadAsync(deactivate)).IsActive);

        using var reactivate = await Send(client, HttpMethod.Put, ItemPath(court.Id, sport.Id), admin.AccessToken, UpdateBody(80_000m, isActive: true));
        await CreateCourtTests.AssertValidationErrorAsync(reactivate, "is_active");
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.CourtSports.AsNoTracking().SingleAsync(cs => cs.CourtId == court.Id);
            Assert.False(stored.IsActive);
            Assert.Equal(90_000m, stored.PricePerHour);
        });
    }

    // ---- delete (deactivate) ----

    [Fact]
    public async Task Delete_DeactivatesButKeepsTheRow_AndIsIdempotent()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var created = await AddAsync(client, admin.AccessToken, court.Id, AddBody(Tennis));

        using var first = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Tennis), owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var afterFirst = await StoredAsync(court.Id, Tennis);
        Assert.False(afterFirst.IsActive);
        Assert.Equal(created.CreatedAtUtc, afterFirst.CreatedAtUtc);
        Assert.True(afterFirst.UpdatedAtUtc > created.UpdatedAtUtc);

        using var second = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Tennis), owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var afterSecond = await StoredAsync(court.Id, Tennis);
        Assert.Equal(afterFirst.UpdatedAtUtc, afterSecond.UpdatedAtUtc);

        await factory.WithDbAsync(async db => Assert.True(await db.Sports.AnyAsync(s => s.Id == Tennis && s.IsActive)));
        var listed = Assert.Single(await ListAsync(client, admin.AccessToken, court.Id));
        Assert.False(listed.IsActive);
        Assert.True(listed.Sport.IsActive);
    }

    [Fact]
    public async Task Delete_RelationshipThatDoesNotExist_Returns204()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        using var unknownSport = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Guid.NewGuid()), admin.AccessToken);
        using var notOffered = await Send(client, HttpMethod.Delete, ItemPath(court.Id, Badminton), admin.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, unknownSport.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, notOffered.StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.CourtSports.AnyAsync(cs => cs.CourtId == court.Id)));
    }

    [Fact]
    public async Task List_WithNoSports_ReturnsEmptyArray()
    {
        using var client = factory.CreateClient();
        var (admin, _, court) = await ArrangeCourtAsync(client);

        Assert.Empty(await ListAsync(client, admin.AccessToken, court.Id));
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

    private static async Task<CourtSportDto> AddAsync(HttpClient client, string token, Guid courtId, object body)
    {
        using var response = await Send(client, HttpMethod.Post, ListPath(courtId), token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<List<CourtSportDto>> ListAsync(HttpClient client, string token, Guid courtId)
    {
        using var response = await Send(client, HttpMethod.Get, ListPath(courtId), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CourtSportDto>>(AuthApi.Json))!;
    }

    private static async Task<CourtSportDto> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<CourtSportDto>(AuthApi.Json))!;

    private async Task<SportMate.Domain.Entities.CourtSport> StoredAsync(Guid courtId, Guid sportId)
    {
        SportMate.Domain.Entities.CourtSport? stored = null;
        await factory.WithDbAsync(async db => stored = await db.CourtSports.AsNoTracking()
            .SingleAsync(cs => cs.CourtId == courtId && cs.SportId == sportId));
        return stored!;
    }

    private sealed record SportDto(Guid Id, string Name, string Slug, bool IsActive);

    private sealed record CourtSportDto(
        SportDto Sport,
        decimal PricePerHour,
        string Currency,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
