using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

public sealed class UpdateCourtTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);

        using var response = await client.PutAsJsonAsync(CourtApi.PathOf(court.Id), CourtApi.UpdateBodyFrom(court), AuthApi.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_ByUser_Returns403AndLeavesCourtUnchanged()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);
        var user = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.User);
        var body = CourtApi.UpdateBodyFrom(court, "inactive");
        body["name"] = "Hijacked";

        using var response = await CourtApi.UpdateAsync(client, user.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStoredAsync(court.Id, stored =>
        {
            Assert.Equal(court.Name, stored.Name);
            Assert.Equal(CourtStatus.Active, stored.Status);
        });
    }

    [Fact]
    public async Task Update_ByAssignedCourtOwner_Returns200AndPersistsAllEditableFields()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var body = new Dictionary<string, object?>
        {
            ["name"] = "  Pickleball Court 02 ",
            ["description"] = null,
            ["address"] = "2 Le Loi, Hai Chau",
            ["city"] = "Da Nang",
            ["latitude"] = 16.054407m,
            ["longitude"] = 108.202167m,
            ["time_zone_id"] = "Asia/Bangkok",
            ["status"] = "inactive"
        };

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await CourtApi.ReadCourtAsync(response);
        Assert.Equal(court.Id, updated.Id);
        Assert.Equal(owner.User.Id, updated.OwnerId);
        Assert.Equal("Pickleball Court 02", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal("inactive", updated.Status);
        Assert.Equal(court.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > court.UpdatedAtUtc);

        await AssertStoredAsync(court.Id, stored =>
        {
            Assert.Equal(owner.User.Id, stored.OwnerId);
            Assert.Equal("Pickleball Court 02", stored.Name);
            Assert.Null(stored.Description);
            Assert.Equal("2 Le Loi, Hai Chau", stored.Address);
            Assert.Equal("Da Nang", stored.City);
            Assert.Equal(16.054407m, stored.Latitude);
            Assert.Equal(108.202167m, stored.Longitude);
            Assert.Equal("Asia/Bangkok", stored.TimeZoneId);
            Assert.Equal(CourtStatus.Inactive, stored.Status);
            Assert.Equal(court.CreatedAtUtc, stored.CreatedAtUtc);
            Assert.Equal(updated.UpdatedAtUtc, stored.UpdatedAtUtc);
        });
    }

    [Fact]
    public async Task Update_ByAnotherCourtOwner_Returns403AndLeavesCourtUnchanged()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.UpdateBodyFrom(court, "inactive");
        body["name"] = "Taken over";

        using var response = await CourtApi.UpdateAsync(client, otherOwner.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStoredAsync(court.Id, stored =>
        {
            Assert.Equal(court.Name, stored.Name);
            Assert.Equal(CourtStatus.Active, stored.Status);
            Assert.Equal(court.UpdatedAtUtc, stored.UpdatedAtUtc);
        });
    }

    [Fact]
    public async Task Update_ByAdmin_Returns200RegardlessOfOwner()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var otherAdmin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var body = CourtApi.UpdateBodyFrom(court);
        body["name"] = "Renamed by Admin";

        using var response = await CourtApi.UpdateAsync(client, otherAdmin.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await CourtApi.ReadCourtAsync(response);
        Assert.Equal("Renamed by Admin", updated.Name);
        Assert.Equal(owner.User.Id, updated.OwnerId);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.CourtOwner)]
    public async Task Update_NonexistentCourt_Returns404(UserRole role)
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var response = await CourtApi.UpdateAsync(client, caller.AccessToken, Guid.NewGuid(), CourtApi.UpdateBodyFrom(court));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("name", null)]
    [InlineData("city", "")]
    [InlineData("address", null)]
    [InlineData("latitude", -90.5)]
    [InlineData("longitude", 181)]
    [InlineData("time_zone_id", "Nowhere/Land")]
    [InlineData("status", null)]
    [InlineData("status", 7)]
    public async Task Update_WithInvalidField_Returns400(string field, object? value)
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var body = CourtApi.UpdateBodyFrom(court);
        body[field] = value;

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, body);

        await CreateCourtTests.AssertValidationErrorAsync(response, field);
        await AssertStoredAsync(court.Id, stored => Assert.Equal(court.UpdatedAtUtc, stored.UpdatedAtUtc));
    }

    [Fact]
    public async Task Update_WithUnknownStatusName_Returns400()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);

        using var response = await CourtApi.UpdateAsync(
            client, owner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, "deleted"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task Status_CanGoFromActiveToInactiveAndBack(UserRole actor)
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var token = actor == UserRole.Admin ? admin.AccessToken : owner.AccessToken;

        using var deactivate = await CourtApi.UpdateAsync(client, token, court.Id, CourtApi.UpdateBodyFrom(court, "inactive"));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal("inactive", (await CourtApi.ReadCourtAsync(deactivate)).Status);
        await AssertStoredAsync(court.Id, stored => Assert.Equal(CourtStatus.Inactive, stored.Status));

        using var activate = await CourtApi.UpdateAsync(client, token, court.Id, CourtApi.UpdateBodyFrom(court, "active"));
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        Assert.Equal("active", (await CourtApi.ReadCourtAsync(activate)).Status);
        await AssertStoredAsync(court.Id, stored => Assert.Equal(CourtStatus.Active, stored.Status));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    public async Task Status_SetToCurrentValue_IsSuccessfulNoOp(string status)
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        if (status == "inactive")
        {
            using var setup = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, status));
            court = await CourtApi.ReadCourtAsync(setup);
        }

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, status));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unchanged = await CourtApi.ReadCourtAsync(response);
        Assert.Equal(status, unchanged.Status);
        Assert.Equal(court.UpdatedAtUtc, unchanged.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_IgnoresClientSuppliedIdAndTimestamps()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var body = CourtApi.UpdateBodyFrom(court);
        body["id"] = Guid.NewGuid();
        body["created_at_utc"] = "2000-01-01T00:00:00Z";
        body["updated_at_utc"] = "2000-01-01T00:00:00Z";

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(court, await CourtApi.ReadCourtAsync(response));
    }

    // ---- owner changes ----

    [Fact]
    public async Task CourtOwner_CannotChangeOwnerId_Returns403()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = otherOwner.User.Id;
        body["name"] = "Handed over";

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertStoredAsync(court.Id, stored =>
        {
            Assert.Equal(owner.User.Id, stored.OwnerId);
            Assert.Equal(court.Name, stored.Name);
        });
    }

    [Fact]
    public async Task CourtOwner_SendingTheirOwnOwnerId_IsNotAnOwnerChange()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = owner.User.Id;
        body["name"] = "Still mine";

        using var response = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Still mine", (await CourtApi.ReadCourtAsync(response)).Name);
    }

    [Fact]
    public async Task Admin_CanReassignOwner_AndOnlyTheNewOwnerCanManage()
    {
        using var client = factory.CreateClient();
        var (admin, oldOwner, court) = await ArrangeCourtAsync(client);
        var newOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = newOwner.User.Id;

        using var response = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await CourtApi.ReadCourtAsync(response);
        Assert.Equal(newOwner.User.Id, updated.OwnerId);
        Assert.True(updated.UpdatedAtUtc > court.UpdatedAtUtc);
        await AssertStoredAsync(court.Id, stored => Assert.Equal(newOwner.User.Id, stored.OwnerId));

        using var oldOwnerGet = await CourtApi.GetAsync(client, oldOwner.AccessToken, court.Id);
        using var newOwnerGet = await CourtApi.GetAsync(client, newOwner.AccessToken, court.Id);
        Assert.Equal(HttpStatusCode.Forbidden, oldOwnerGet.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newOwnerGet.StatusCode);
    }

    [Fact]
    public async Task Admin_ReassigningToNonexistentUser_Returns400()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = Guid.NewGuid();

        using var response = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, body);

        await CreateCourtTests.AssertValidationErrorAsync(response, "owner_id");
        await AssertStoredAsync(court.Id, stored => Assert.Equal(owner.User.Id, stored.OwnerId));
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Admin_ReassigningToNonCourtOwner_Returns400(UserRole candidateRole)
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var candidate = await CourtApi.LoginWithRoleAsync(factory, client, candidateRole);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = candidate.User.Id;

        using var response = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, body);

        await CreateCourtTests.AssertValidationErrorAsync(response, "owner_id");
        await AssertStoredAsync(court.Id, stored => Assert.Equal(owner.User.Id, stored.OwnerId));
    }

    [Fact]
    public async Task Admin_ReassigningToSuspendedCourtOwner_Returns400()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        var suspended = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        await factory.SuspendAsync(suspended.User.Id);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = suspended.User.Id;

        using var response = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, body);

        await CreateCourtTests.AssertValidationErrorAsync(response, "owner_id");
        await AssertStoredAsync(court.Id, stored => Assert.Equal(owner.User.Id, stored.OwnerId));
    }

    [Fact]
    public async Task Update_WithoutOwnerId_KeepsCurrentOwner()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);

        using var response = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(owner.User.Id, (await CourtApi.ReadCourtAsync(response)).OwnerId);
    }

    private async Task<(AuthApi.AuthDto Admin, AuthApi.AuthDto Owner, CourtApi.CourtDto Court)> ArrangeCourtAsync(
        HttpClient client)
    {
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var court = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);
        return (admin, owner, court);
    }

    private Task AssertStoredAsync(Guid courtId, Action<SportMate.Domain.Entities.Court> assert) =>
        factory.WithDbAsync(async db => assert(await db.Courts.AsNoTracking().SingleAsync(c => c.Id == courtId)));
}
