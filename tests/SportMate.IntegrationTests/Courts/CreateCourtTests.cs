using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

public sealed class CreateCourtTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await AuthApi.PostAsync(client, CourtApi.BasePath, CourtApi.ValidCreateBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    public async Task Create_ByNonAdmin_Returns403AndCreatesNothing(UserRole role)
    {
        using var client = factory.CreateClient();
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);

        // A CourtOwner naming itself as owner must not bypass the Admin requirement.
        using var response = await CourtApi.CreateAsync(
            client, caller.AccessToken, CourtApi.ValidCreateBody(role == UserRole.CourtOwner ? caller.User.Id : owner.User.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await factory.WithDbAsync(async db =>
            Assert.False(await db.Courts.AnyAsync(c => c.OwnerId == caller.User.Id || c.OwnerId == owner.User.Id)));
    }

    [Fact]
    public async Task Create_ByAdmin_Returns201WithLocationAndPersistsCourt()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var before = DateTime.UtcNow;

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(owner.User.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var court = await CourtApi.ReadCourtAsync(response);
        Assert.Equal($"/api/v1/courts/{court.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(owner.User.Id, court.OwnerId);
        Assert.Equal("Tennis Court 01", court.Name);
        Assert.Equal("Outdoor hard court", court.Description);
        Assert.Equal("1 Tran Phu, Ba Dinh", court.Address);
        Assert.Equal("Ha Noi", court.City);
        Assert.Equal(21.028511m, court.Latitude);
        Assert.Equal(105.804817m, court.Longitude);
        Assert.Equal("Asia/Ho_Chi_Minh", court.TimeZoneId);
        Assert.Equal("active", court.Status);
        Assert.Equal(court.CreatedAtUtc, court.UpdatedAtUtc);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Courts.AsNoTracking().SingleAsync(c => c.Id == court.Id);
            Assert.Equal(owner.User.Id, stored.OwnerId);
            Assert.NotEqual(admin.User.Id, stored.OwnerId);
            Assert.Equal("Ha Noi", stored.City);
            Assert.Equal(CourtStatus.Active, stored.Status);
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAtUtc.Kind);
            Assert.InRange(stored.CreatedAtUtc, before, DateTime.UtcNow);
            Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        });
    }

    [Fact]
    public async Task Create_WithoutOwnerId_Returns400()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(null));

        await AssertValidationErrorAsync(response, "owner_id");
    }

    [Fact]
    public async Task Create_WithNonexistentOwner_Returns400()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(Guid.NewGuid()));

        await AssertValidationErrorAsync(response, "owner_id");
    }

    [Fact]
    public async Task Create_WithSuspendedCourtOwner_Returns400()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        await factory.SuspendAsync(owner.User.Id);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(owner.User.Id));

        await AssertValidationErrorAsync(response, "owner_id");
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Create_WithOwnerThatIsNotCourtOwner_Returns400(UserRole ownerRole)
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var candidate = await CourtApi.LoginWithRoleAsync(factory, client, ownerRole);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(candidate.User.Id));

        await AssertValidationErrorAsync(response, "owner_id");
    }

    [Fact]
    public async Task Create_AdminCannotAssignThemselves()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, CourtApi.ValidCreateBody(admin.User.Id));

        await AssertValidationErrorAsync(response, "owner_id");
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("address", "   ")]
    [InlineData("city", null)]
    [InlineData("latitude", 91)]
    [InlineData("longitude", -180.5)]
    [InlineData("latitude", null)]
    [InlineData("time_zone_id", "Mars/Olympus_Mons")]
    [InlineData("time_zone_id", "SE Asia Standard Time")]
    public async Task Create_WithInvalidField_Returns400ForThatField(string field, object? value)
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.ValidCreateBody(owner.User.Id);
        body[field] = value;

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, body);

        await AssertValidationErrorAsync(response, field);
        await factory.WithDbAsync(async db => Assert.False(await db.Courts.AnyAsync(c => c.OwnerId == owner.User.Id)));
    }

    [Fact]
    public async Task Create_WithTooLongDescription_Returns400()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.ValidCreateBody(owner.User.Id);
        body["description"] = new string('d', 1001);

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, body);

        await AssertValidationErrorAsync(response, "description");
    }

    [Fact]
    public async Task Create_IgnoresClientSuppliedStatusAndTimestamps()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.ValidCreateBody(owner.User.Id);
        body["id"] = Guid.NewGuid();
        body["status"] = "inactive";
        body["created_at_utc"] = "2000-01-01T00:00:00Z";

        using var response = await CourtApi.CreateAsync(client, admin.AccessToken, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var court = await CourtApi.ReadCourtAsync(response);
        Assert.NotEqual((Guid)body["id"]!, court.Id);
        Assert.Equal("active", court.Status);
        Assert.True(court.CreatedAtUtc.Year > 2000);
    }

    internal static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, key => key == field || key.EndsWith(field, StringComparison.OrdinalIgnoreCase));
    }
}
