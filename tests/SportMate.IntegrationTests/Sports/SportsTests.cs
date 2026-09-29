using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Sports;

public sealed class SportsTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string Path = "/api/v1/sports";

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task List_IsAvailableToEveryRole(UserRole role)
    {
        using var client = factory.CreateClient();
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, caller.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_ContainsAllSevenSeedSportsWithStableIdsAndSlugs()
    {
        var sports = await ListAsync();

        foreach (var seed in SportSeed.Sports)
        {
            Assert.Contains(new SportDto(seed.Id, seed.Name, seed.Slug), sports);
        }

        Assert.Equal(
            ["badminton", "basketball", "football", "pickleball", "table-tennis", "tennis", "volleyball"],
            SportSeed.Sports.Select(s => s.Slug).Order());
    }

    [Fact]
    public async Task List_ReturnsOnlyActiveSports()
    {
        var inactive = await factory.AddSportAsync(isActive: false);
        var active = await factory.AddSportAsync(isActive: true);

        var sports = await ListAsync();

        Assert.DoesNotContain(sports, s => s.Id == inactive.Id);
        Assert.Contains(sports, s => s.Id == active.Id);
    }

    [Fact]
    public async Task List_HidesASportOnceItIsDeactivated()
    {
        var sport = await factory.AddSportAsync();
        Assert.Contains(await ListAsync(), s => s.Id == sport.Id);

        await factory.SetSportActiveAsync(sport.Id, false);

        Assert.DoesNotContain(await ListAsync(), s => s.Id == sport.Id);
        await factory.WithDbAsync(async db => Assert.True(await db.Sports.AnyAsync(s => s.Id == sport.Id)));
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        var sports = await ListAsync();

        Assert.Equal(sports.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => s.Id), sports.Select(s => s.Id));
        Assert.Equal(sports, await ListAsync());
    }

    [Fact]
    public async Task Slug_MustBeUnique()
    {
        var existing = SportSeed.Sports[0];
        var duplicate = new Sport($"Other {Guid.NewGuid():N}", existing.Slug, DateTime.UtcNow);

        await factory.WithDbAsync(async db =>
        {
            db.Sports.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Name_MustBeUnique()
    {
        var duplicate = new Sport("tennis", $"other-{Guid.NewGuid():N}", DateTime.UtcNow);

        await factory.WithDbAsync(async db =>
        {
            db.Sports.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    private async Task<List<SportDto>> ListAsync()
    {
        using var client = factory.CreateClient();
        var caller = await AuthApi.RegisterAndLoginAsync(client);
        using var response = await client.SendAsync(AuthApi.Authorized(HttpMethod.Get, Path, caller.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<SportDto>>(AuthApi.Json))!;
    }

    private sealed record SportDto(Guid Id, string Name, string Slug);
}
