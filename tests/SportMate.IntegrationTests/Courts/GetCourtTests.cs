using System.Net;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.Courts;

public sealed class GetCourtTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var court = await CreateCourtAsync(client);

        using var response = await client.GetAsync(CourtApi.PathOf(court.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ByUser_Returns403()
    {
        using var client = factory.CreateClient();
        var court = await CreateCourtAsync(client);
        var user = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.User);

        using var response = await CourtApi.GetAsync(client, user.AccessToken, court.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ByAssignedCourtOwner_Returns200()
    {
        using var client = factory.CreateClient();
        var owner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var court = await CreateCourtAsync(client, owner.User.Id);

        using var response = await CourtApi.GetAsync(client, owner.AccessToken, court.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(court, await CourtApi.ReadCourtAsync(response));
    }

    [Fact]
    public async Task Get_ByAnotherCourtOwner_Returns403()
    {
        using var client = factory.CreateClient();
        var court = await CreateCourtAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);

        using var response = await CourtApi.GetAsync(client, otherOwner.AccessToken, court.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ByAdmin_Returns200RegardlessOfOwner()
    {
        using var client = factory.CreateClient();
        var court = await CreateCourtAsync(client);
        var otherAdmin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);

        using var response = await CourtApi.GetAsync(client, otherAdmin.AccessToken, court.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(court, await CourtApi.ReadCourtAsync(response));
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.CourtOwner)]
    public async Task Get_NonexistentCourt_Returns404(UserRole role)
    {
        using var client = factory.CreateClient();
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        using var response = await CourtApi.GetAsync(client, caller.AccessToken, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<CourtApi.CourtDto> CreateCourtAsync(HttpClient client, Guid? ownerId = null)
    {
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        ownerId ??= (await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner)).User.Id;
        return await CourtApi.CreateCourtAsync(client, admin.AccessToken, ownerId.Value);
    }
}
