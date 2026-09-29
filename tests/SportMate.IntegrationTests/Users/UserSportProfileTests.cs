using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;
using SportMate.IntegrationTests.Courts;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Users;

public sealed class UserSportProfileTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string MySports = "/api/v1/users/me/sports";
    private static readonly Guid Tennis = SportSeed.Sports.Single(s => s.Slug == "tennis").Id;
    private static readonly Guid Badminton = SportSeed.Sports.Single(s => s.Slug == "badminton").Id;
    private static readonly Guid Volleyball = SportSeed.Sports.Single(s => s.Slug == "volleyball").Id;

    private static string PathOf(Guid sportId) => $"{MySports}/{sportId}";

    // ---- authentication / roles ----

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();

        using var get = await client.GetAsync(MySports);
        using var put = await client.PutAsJsonAsync(PathOf(Tennis), new { skill_level = 1 }, AuthApi.Json);
        using var delete = await client.DeleteAsync(PathOf(Tennis));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task EveryRole_CanManageTheirOwnProfiles(UserRole role)
    {
        using var client = factory.CreateClient();
        var token = (await CourtApi.LoginWithRoleAsync(factory, client, role)).AccessToken;

        using var put = await PutAsync(client, token, Tennis, new { skill_level = "advanced" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Single(await ListAsync(client, token));

        using var delete = await DeleteAsync(client, token, Tennis);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await ListAsync(client, token));
    }

    // ---- GET ----

    [Fact]
    public async Task Get_WithNoProfiles_ReturnsEmptyArray()
    {
        using var client = factory.CreateClient();
        var token = (await AuthApi.RegisterAndLoginAsync(client)).AccessToken;

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MySports, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Get_ReturnsMultipleProfilesOrderedBySportName()
    {
        using var client = factory.CreateClient();
        var token = (await AuthApi.RegisterAndLoginAsync(client)).AccessToken;
        foreach (var (sportId, level) in new[] { (Volleyball, 5), (Tennis, 3), (Badminton, 1) })
        {
            using var put = await PutAsync(client, token, sportId, new { skill_level = level });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        var profiles = await ListAsync(client, token);

        Assert.Equal(["Badminton", "Tennis", "Volleyball"], profiles.Select(p => p.Sport.Name));
        Assert.Equal(["beginner", "intermediate", "expert"], profiles.Select(p => p.SkillLevel));
        Assert.Equal(new ProfileSportDto(Tennis, "Tennis", "tennis", true), profiles[1].Sport);
        Assert.Equal(profiles, await ListAsync(client, token));
    }

    [Fact]
    public async Task Get_ExistingProfileOfDeactivatedSport_IsReturnedWithIsActiveFalse()
    {
        using var client = factory.CreateClient();
        var token = (await AuthApi.RegisterAndLoginAsync(client)).AccessToken;
        var sport = await factory.AddSportAsync();
        using (var put = await PutAsync(client, token, sport.Id, new { skill_level = 2 }))
        {
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        await factory.SetSportActiveAsync(sport.Id, false);

        var profile = Assert.Single(await ListAsync(client, token));
        Assert.Equal(sport.Id, profile.Sport.Id);
        Assert.Equal(sport.Slug, profile.Sport.Slug);
        Assert.False(profile.Sport.IsActive);
        Assert.Equal("elementary", profile.SkillLevel);
    }

    // ---- PUT ----

    [Fact]
    public async Task Put_CreatesProfileForActiveSport()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await ReadProfileAsync(response);
        Assert.Equal(new ProfileSportDto(Tennis, "Tennis", "tennis", true), profile.Sport);
        Assert.Equal("intermediate", profile.SkillLevel);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.UserSportProfiles.AsNoTracking().SingleAsync(p => p.UserId == auth.User.Id);
            Assert.Equal(Tennis, stored.SportId);
            Assert.Equal(SkillLevel.Intermediate, stored.SkillLevel);
            Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        });
    }

    [Theory]
    [InlineData(1, "beginner")]
    [InlineData(2, "elementary")]
    [InlineData(3, "intermediate")]
    [InlineData(4, "advanced")]
    [InlineData(5, "expert")]
    public async Task Put_AcceptsNumericAndNamedSkillLevels(int number, string name)
    {
        using var client = factory.CreateClient();
        var token = (await AuthApi.RegisterAndLoginAsync(client)).AccessToken;

        using var numeric = await PutAsync(client, token, Tennis, new { skill_level = number });
        using var named = await PutAsync(client, token, Badminton, new { skill_level = name });

        Assert.Equal(name, (await ReadProfileAsync(numeric)).SkillLevel);
        Assert.Equal(name, (await ReadProfileAsync(named)).SkillLevel);
    }

    [Fact]
    public async Task Put_UpdatesExistingProfile_KeepingCreatedAt()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        using (await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = "beginner" }))
        {
        }

        var before = await StoredProfileAsync(auth.User.Id, Tennis);

        using var response = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = "expert" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("expert", (await ReadProfileAsync(response)).SkillLevel);
        var after = await StoredProfileAsync(auth.User.Id, Tennis);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(SkillLevel.Expert, after.SkillLevel);
        Assert.Equal(before.CreatedAtUtc, after.CreatedAtUtc);
        Assert.True(after.UpdatedAtUtc > before.UpdatedAtUtc);
    }

    [Fact]
    public async Task Put_RepeatedWithSameBody_IsIdempotent()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        using var first = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = 4 });
        var afterFirst = await StoredProfileAsync(auth.User.Id, Tennis);

        using var second = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = 4 });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await ReadProfileAsync(first), await ReadProfileAsync(second));
        var afterSecond = await StoredProfileAsync(auth.User.Id, Tennis);
        Assert.Equal(afterFirst.Id, afterSecond.Id);
        Assert.Equal(afterFirst.UpdatedAtUtc, afterSecond.UpdatedAtUtc);
        await factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.UserSportProfiles.CountAsync(p => p.UserId == auth.User.Id)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public async Task Put_WithInvalidSkillLevel_Returns400(int? value)
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = value });

        await CreateCourtTests.AssertValidationErrorAsync(response, "skill_level");
        await AssertNoProfilesAsync(auth.User.Id);
    }

    [Theory]
    [InlineData("novice")]
    [InlineData("pro")]
    public async Task Put_WithUnknownSkillLevelName_Returns400(string value)
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoProfilesAsync(auth.User.Id);
    }

    [Fact]
    public async Task Put_WithNonexistentSport_Returns404()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PutAsync(client, auth.AccessToken, Guid.NewGuid(), new { skill_level = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNoProfilesAsync(auth.User.Id);
    }

    [Fact]
    public async Task Put_NewProfileForInactiveSport_Returns400()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var inactive = await factory.AddSportAsync(isActive: false);

        using var response = await PutAsync(client, auth.AccessToken, inactive.Id, new { skill_level = 1 });

        await CreateCourtTests.AssertValidationErrorAsync(response, "sport_id");
        await AssertNoProfilesAsync(auth.User.Id);
    }

    [Fact]
    public async Task Put_ExistingProfileOfDeactivatedSport_CanStillBeUpdated()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var sport = await factory.AddSportAsync();
        using (await PutAsync(client, auth.AccessToken, sport.Id, new { skill_level = 1 }))
        {
        }

        await factory.SetSportActiveAsync(sport.Id, false);

        using var response = await PutAsync(client, auth.AccessToken, sport.Id, new { skill_level = "advanced" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await ReadProfileAsync(response);
        Assert.Equal("advanced", profile.SkillLevel);
        Assert.False(profile.Sport.IsActive);
        Assert.Equal(SkillLevel.Advanced, (await StoredProfileAsync(auth.User.Id, sport.Id)).SkillLevel);
    }

    [Fact]
    public async Task DeactivatingASport_KeepsExistingProfiles()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var sport = await factory.AddSportAsync();
        using (await PutAsync(client, auth.AccessToken, sport.Id, new { skill_level = 3 }))
        {
        }

        await factory.SetSportActiveAsync(sport.Id, false);

        Assert.Equal(SkillLevel.Intermediate, (await StoredProfileAsync(auth.User.Id, sport.Id)).SkillLevel);
    }

    // ---- DELETE ----

    [Fact]
    public async Task Delete_ExistingProfile_Returns204AndKeepsTheSport()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        using (await PutAsync(client, auth.AccessToken, Tennis, new { skill_level = 2 }))
        {
        }

        using var response = await DeleteAsync(client, auth.AccessToken, Tennis);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertNoProfilesAsync(auth.User.Id);
        await factory.WithDbAsync(async db => Assert.True(await db.Sports.AnyAsync(s => s.Id == Tennis && s.IsActive)));
    }

    [Fact]
    public async Task Delete_MissingProfileOrUnknownSport_Returns204()
    {
        using var client = factory.CreateClient();
        var token = (await AuthApi.RegisterAndLoginAsync(client)).AccessToken;

        using var missingProfile = await DeleteAsync(client, token, Badminton);
        using var unknownSport = await DeleteAsync(client, token, Guid.NewGuid());
        using var again = await DeleteAsync(client, token, Badminton);

        Assert.Equal(HttpStatusCode.NoContent, missingProfile.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknownSport.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingProfileOfDeactivatedSport_Returns204()
    {
        using var client = factory.CreateClient();
        var auth = await AuthApi.RegisterAndLoginAsync(client);
        var sport = await factory.AddSportAsync();
        using (await PutAsync(client, auth.AccessToken, sport.Id, new { skill_level = 1 }))
        {
        }

        await factory.SetSportActiveAsync(sport.Id, false);

        using var response = await DeleteAsync(client, auth.AccessToken, sport.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertNoProfilesAsync(auth.User.Id);
    }

    // ---- isolation between users ----

    [Fact]
    public async Task Users_OnlySeeAndChangeTheirOwnProfiles()
    {
        using var client = factory.CreateClient();
        var alice = await AuthApi.RegisterAndLoginAsync(client);
        var bob = await AuthApi.RegisterAndLoginAsync(client);
        using (await PutAsync(client, alice.AccessToken, Tennis, new { skill_level = "expert" }))
        {
        }

        Assert.Empty(await ListAsync(client, bob.AccessToken));

        // Bob's PUT for the same sport creates Bob's own profile and leaves Alice's untouched.
        using (var bobPut = await PutAsync(client, bob.AccessToken, Tennis, new { skill_level = "beginner" }))
        {
            Assert.Equal(HttpStatusCode.OK, bobPut.StatusCode);
        }

        // Bob's DELETE removes only Bob's profile.
        using (var bobDelete = await DeleteAsync(client, bob.AccessToken, Tennis))
        {
            Assert.Equal(HttpStatusCode.NoContent, bobDelete.StatusCode);
        }

        var aliceProfile = Assert.Single(await ListAsync(client, alice.AccessToken));
        Assert.Equal("expert", aliceProfile.SkillLevel);
        Assert.Equal(SkillLevel.Expert, (await StoredProfileAsync(alice.User.Id, Tennis)).SkillLevel);
        await AssertNoProfilesAsync(bob.User.Id);
    }

    [Fact]
    public async Task ClientSuppliedIdentityAndTimestamps_AreIgnored()
    {
        using var client = factory.CreateClient();
        var alice = await AuthApi.RegisterAndLoginAsync(client);
        var bob = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await PutAsync(client, bob.AccessToken, Tennis, new
        {
            skill_level = 3,
            user_id = alice.User.Id,
            id = Guid.NewGuid(),
            created_at_utc = "2000-01-01T00:00:00Z",
            updated_at_utc = "2000-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertNoProfilesAsync(alice.User.Id);
        var stored = await StoredProfileAsync(bob.User.Id, Tennis);
        Assert.True(stored.CreatedAtUtc.Year > 2000);
        Assert.True(stored.UpdatedAtUtc.Year > 2000);
    }

    // ---- helpers ----

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string token, Guid sportId, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Put, PathOf(sportId), token, body));

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string token, Guid sportId) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Delete, PathOf(sportId), token));

    private static async Task<List<ProfileDto>> ListAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, MySports, token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ProfileDto>>(AuthApi.Json))!;
    }

    private static async Task<ProfileDto> ReadProfileAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProfileDto>(AuthApi.Json))!;
    }

    private async Task<SportMate.Domain.Entities.UserSportProfile> StoredProfileAsync(Guid userId, Guid sportId)
    {
        SportMate.Domain.Entities.UserSportProfile? profile = null;
        await factory.WithDbAsync(async db => profile = await db.UserSportProfiles.AsNoTracking()
            .SingleAsync(p => p.UserId == userId && p.SportId == sportId));
        return profile!;
    }

    private Task AssertNoProfilesAsync(Guid userId) => factory.WithDbAsync(async db =>
        Assert.False(await db.UserSportProfiles.AnyAsync(p => p.UserId == userId)));

    private sealed record ProfileSportDto(Guid Id, string Name, string Slug, bool IsActive);

    private sealed record ProfileDto(ProfileSportDto Sport, string SkillLevel);
}
