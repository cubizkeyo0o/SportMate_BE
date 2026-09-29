using System.Net;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

/// <summary>
/// Ownership is decided from the authenticated caller and the stored court owner, with the caller's
/// current database role; nothing in the request body can grant access.
/// </summary>
public sealed class CourtOwnershipSecurityTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task OtherCourtOwner_ClaimingOwnershipInBody_CannotModifyCourt()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var attacker = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);

        foreach (var claimedOwner in new[] { attacker.User.Id, owner.User.Id })
        {
            var body = CourtApi.UpdateBodyFrom(court, "inactive");
            body["owner_id"] = claimedOwner;

            using var response = await CourtApi.UpdateAsync(client, attacker.AccessToken, court.Id, body);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Courts.AsNoTracking().SingleAsync(c => c.Id == court.Id);
            Assert.Equal(owner.User.Id, stored.OwnerId);
            Assert.Equal(CourtStatus.Active, stored.Status);
        });
    }

    [Fact]
    public async Task User_ClaimingOwnershipInBody_CannotModifyCourt()
    {
        using var client = factory.CreateClient();
        var (_, _, court) = await ArrangeCourtAsync(client);
        var user = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.User);
        var body = CourtApi.UpdateBodyFrom(court);
        body["owner_id"] = user.User.Id;

        using var response = await CourtApi.UpdateAsync(client, user.AccessToken, court.Id, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CourtOwner_ManagesOnlyTheirOwnCourts()
    {
        using var client = factory.CreateClient();
        var (admin, ownerA, courtA) = await ArrangeCourtAsync(client);
        var ownerB = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var courtB = await CourtApi.CreateCourtAsync(client, admin.AccessToken, ownerB.User.Id);

        using var aOnA = await CourtApi.UpdateAsync(client, ownerA.AccessToken, courtA.Id, CourtApi.UpdateBodyFrom(courtA));
        using var aOnB = await CourtApi.UpdateAsync(client, ownerA.AccessToken, courtB.Id, CourtApi.UpdateBodyFrom(courtB));
        using var bOnA = await CourtApi.UpdateAsync(client, ownerB.AccessToken, courtA.Id, CourtApi.UpdateBodyFrom(courtA));
        using var bOnB = await CourtApi.UpdateAsync(client, ownerB.AccessToken, courtB.Id, CourtApi.UpdateBodyFrom(courtB));

        Assert.Equal(HttpStatusCode.OK, aOnA.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, aOnB.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bOnA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bOnB.StatusCode);
    }

    [Fact]
    public async Task Admin_ManagesCourtsTheyDoNotOwn()
    {
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        var unrelatedAdmin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var get = await CourtApi.GetAsync(client, unrelatedAdmin.AccessToken, court.Id);
        using var put = await CourtApi.UpdateAsync(
            client, unrelatedAdmin.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, "inactive"));

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(owner.User.Id, (await CourtApi.ReadCourtAsync(put)).OwnerId);
    }

    [Fact]
    public async Task DemotedOwner_ImmediatelyLosesAccess_CourtStaysIntact_AndAdminCanReassign()
    {
        using var client = factory.CreateClient();
        var (admin, owner, court) = await ArrangeCourtAsync(client);
        using var before = await CourtApi.UpdateAsync(client, owner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await factory.ChangeRoleAsync(owner.User.Id, UserRole.User);

        // Same access token, which still says role=CourtOwner.
        using var update = await CourtApi.UpdateAsync(
            client, owner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, "inactive"));
        using var get = await CourtApi.GetAsync(client, owner.AccessToken, court.Id);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Courts.AsNoTracking().SingleAsync(c => c.Id == court.Id);
            Assert.Equal(owner.User.Id, stored.OwnerId);
            Assert.Equal(CourtStatus.Active, stored.Status);
            Assert.Equal(court.Name, stored.Name);
        });

        var newOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var reassign = CourtApi.UpdateBodyFrom(court);
        reassign["owner_id"] = newOwner.User.Id;
        using var reassigned = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, reassign);
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);
        Assert.Equal(newOwner.User.Id, (await CourtApi.ReadCourtAsync(reassigned)).OwnerId);

        using var newOwnerUpdate = await CourtApi.UpdateAsync(
            client, newOwner.AccessToken, court.Id, CourtApi.UpdateBodyFrom(court, "inactive"));
        Assert.Equal(HttpStatusCode.OK, newOwnerUpdate.StatusCode);

        // The demoted user cannot be assigned again while they are not a CourtOwner.
        var back = CourtApi.UpdateBodyFrom(court);
        back["owner_id"] = owner.User.Id;
        using var backToDemoted = await CourtApi.UpdateAsync(client, admin.AccessToken, court.Id, back);
        await CreateCourtTests.AssertValidationErrorAsync(backToDemoted, "owner_id");
    }

    [Fact]
    public async Task OwnerPromotedToAdmin_KeepsAccessAsAdmin()
    {
        // Admin authorization does not depend on ownership, so this is the Admin path, not the owner path.
        using var client = factory.CreateClient();
        var (_, owner, court) = await ArrangeCourtAsync(client);
        await factory.ChangeRoleAsync(owner.User.Id, UserRole.Admin);

        using var response = await CourtApi.GetAsync(client, owner.AccessToken, court.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(AuthApi.AuthDto Admin, AuthApi.AuthDto Owner, CourtApi.CourtDto Court)> ArrangeCourtAsync(
        HttpClient client)
    {
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var court = await CourtApi.CreateCourtAsync(client, admin.AccessToken, owner.User.Id);
        return (admin, owner, court);
    }
}
