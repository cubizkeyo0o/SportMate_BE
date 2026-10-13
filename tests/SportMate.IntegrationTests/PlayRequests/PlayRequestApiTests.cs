using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence.Seed;
using SportMate.IntegrationTests.Courts;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.PlayRequests;

public sealed class PlayRequestApiTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private const string BasePath = "/api/v1/play-requests";
    private const string MySports = "/api/v1/users/me/sports";
    private static readonly Guid Tennis = SportSeed.Sports.Single(sport => sport.Slug == "tennis").Id;

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        using var client = factory.CreateClient();
        using var list = await client.GetAsync(BasePath);
        using var create = await AuthApi.PostAsync(client, BasePath, ValidBody());

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task EveryRole_CanCreateTheirOwnRequest(UserRole role)
    {
        using var client = factory.CreateClient();
        var creator = await CourtApi.LoginWithRoleAsync(factory, client, role);
        await SetSportProfileAsync(client, creator.AccessToken, Tennis, "expert");

        using var response = await CreateAsync(client, creator.AccessToken, ValidBody(min: "beginner", max: "intermediate"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var request = await ReadAsync(response);
        Assert.Equal($"{BasePath}/{request.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(creator.User.DisplayName, request.Host.DisplayName);
        Assert.Equal("expert", request.Host.SkillLevel);
        Assert.Equal("open", request.Status);
    }

    [Fact]
    public async Task Create_RequiresActiveSportProfileAndFutureStart()
    {
        using var client = factory.CreateClient();
        var creator = await AuthApi.RegisterAndLoginAsync(client);

        using var noProfile = await CreateAsync(client, creator.AccessToken, ValidBody());
        Assert.Equal(HttpStatusCode.Conflict, noProfile.StatusCode);

        await SetSportProfileAsync(client, creator.AccessToken, Tennis, "intermediate");
        var pastBody = ValidBody();
        pastBody["starts_at"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
        using var past = await CreateAsync(client, creator.AccessToken, pastBody);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        await AssertValidationErrorAsync(past, "starts_at");
    }

    [Fact]
    public async Task Discovery_MatchesInclusiveSkillBoundsAndHidesPrivateFields()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var eligible = await AuthApi.RegisterAndLoginAsync(client);
        var tooAdvanced = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "expert");
        await SetSportProfileAsync(client, eligible.AccessToken, Tennis, "intermediate");
        await SetSportProfileAsync(client, tooAdvanced.AccessToken, Tennis, "advanced");
        var body = ValidBody(min: "intermediate", max: "intermediate");
        body["latitude"] = 21.028511m;
        body["longitude"] = 105.804817m;
        var request = await CreateRequestAsync(client, host.AccessToken, body);

        using var discovery = await GetAuthorizedAsync(client, eligible.AccessToken, BasePath);
        using var hidden = await GetAuthorizedAsync(client, tooAdvanced.AccessToken, $"{BasePath}/{request.Id}");
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var page = await ReadPageAsync(discovery);
        var match = Assert.Single(page.Items, item => item.Id == request.Id);
        Assert.Equal(0, match.AcceptedCount);
        Assert.Equal("expert", match.Host.SkillLevel);

        using var json = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync());
        var bodyText = json.RootElement.GetRawText();
        Assert.DoesNotContain(host.User.Email, bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlayerWithoutSportProfile_CannotDiscoverOrJoinRequest()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var player = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "intermediate");
        var request = await CreateRequestAsync(client, host.AccessToken, ValidBody());

        using var hidden = await GetAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}");
        using var join = await PostAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}/participants");

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
    }

    [Fact]
    public async Task JoinRejectAndResubmit_ReuseParticipantRecord_AndHostAloneCanRespond()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var player = await AuthApi.RegisterAndLoginAsync(client);
        var outsider = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "advanced");
        await SetSportProfileAsync(client, player.AccessToken, Tennis, "intermediate");
        await SetSportProfileAsync(client, outsider.AccessToken, Tennis, "intermediate");
        var request = await CreateRequestAsync(client, host.AccessToken, ValidBody());

        using var join = await PostAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}/participants");
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var pending = await ReadAsync(join);
        Assert.Equal("pending", pending.MyParticipantStatus);

        using var visibleToPlayer = await GetAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}");
        var playerView = await ReadAsync(visibleToPlayer);
        Assert.Empty(playerView.Participants);

        var participant = Assert.Single(await ReadParticipantsAsync(client, host.AccessToken, request.Id));
        Assert.Equal(player.User.DisplayName, participant.DisplayName);
        Assert.Equal("intermediate", participant.SkillLevel);
        Assert.Equal("pending", participant.Status);
        using var duplicateJoin = await PostAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}/participants");
        Assert.Equal(HttpStatusCode.Conflict, duplicateJoin.StatusCode);
        using var hostJoin = await PostAuthorizedAsync(client, host.AccessToken, $"{BasePath}/{request.Id}/participants");
        Assert.Equal(HttpStatusCode.Conflict, hostJoin.StatusCode);

        using var outsiderAccept = await PostAuthorizedAsync(
            client, outsider.AccessToken, $"{BasePath}/{request.Id}/participants/{participant.Id}/accept");
        Assert.Equal(HttpStatusCode.NotFound, outsiderAccept.StatusCode);

        using var reject = await PostAuthorizedAsync(
            client, host.AccessToken, $"{BasePath}/{request.Id}/participants/{participant.Id}/reject");
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        using var resubmit = await PostAuthorizedAsync(client, player.AccessToken, $"{BasePath}/{request.Id}/participants");
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);

        var reused = Assert.Single(await ReadParticipantsAsync(client, host.AccessToken, request.Id));
        Assert.Equal(participant.Id, reused.Id);
        Assert.Equal("pending", reused.Status);
    }

    [Fact]
    public async Task FullRequest_RemainsVisibleButRejectsNewJoin_AndAcceptedWithdrawalReopensIt()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var acceptedPlayer = await AuthApi.RegisterAndLoginAsync(client);
        var waitingPlayer = await AuthApi.RegisterAndLoginAsync(client);
        var newPlayer = await AuthApi.RegisterAndLoginAsync(client);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(user => user.Id == acceptedPlayer.User.Id))
                .UpdateProfile("Accepted Player", null, null, DateTime.UtcNow);
            (await db.Users.SingleAsync(user => user.Id == waitingPlayer.User.Id))
                .UpdateProfile("Waiting Player", null, null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        foreach (var auth in new[] { host, acceptedPlayer, waitingPlayer, newPlayer })
        {
            await SetSportProfileAsync(client, auth.AccessToken, Tennis, "intermediate");
        }

        var body = ValidBody(spots: 1);
        var request = await CreateRequestAsync(client, host.AccessToken, body);
        await JoinAsync(client, acceptedPlayer.AccessToken, request.Id);
        await JoinAsync(client, waitingPlayer.AccessToken, request.Id);
        var participantList = await ReadParticipantsAsync(client, host.AccessToken, request.Id);
        var accepted = participantList.Single(participant => participant.DisplayName == "Accepted Player");
        await EnsureStatusAsync(
            await PostAuthorizedAsync(client, host.AccessToken, $"{BasePath}/{request.Id}/participants/{accepted.Id}/accept"),
            HttpStatusCode.OK);

        using var detail = await GetAuthorizedAsync(client, newPlayer.AccessToken, $"{BasePath}/{request.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("full", (await ReadAsync(detail)).Status);
        using var cannotJoin = await PostAuthorizedAsync(client, newPlayer.AccessToken, $"{BasePath}/{request.Id}/participants");
        Assert.Equal(HttpStatusCode.Conflict, cannotJoin.StatusCode);

        using var withdraw = await PostAuthorizedAsync(client, acceptedPlayer.AccessToken, $"{BasePath}/{request.Id}/withdraw");
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        Assert.Equal("open", (await ReadAsync(withdraw)).Status);
    }

    [Fact]
    public async Task MatchingFieldsAreImmutableAfterJoin_ButDescriptionCanChange()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var player = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "intermediate");
        await SetSportProfileAsync(client, player.AccessToken, Tennis, "intermediate");
        var request = await CreateRequestAsync(client, host.AccessToken, ValidBody());
        await JoinAsync(client, player.AccessToken, request.Id);

        using var changeTitle = await PutAuthorizedAsync(client, host.AccessToken, $"{BasePath}/{request.Id}",
            UpdateBody(request, title: "Changed"));
        Assert.Equal(HttpStatusCode.Conflict, changeTitle.StatusCode);

        using var changeDescription = await PutAuthorizedAsync(client, host.AccessToken, $"{BasePath}/{request.Id}",
            UpdateBody(request, description: "Updated details"));
        Assert.Equal(HttpStatusCode.OK, changeDescription.StatusCode);
        Assert.Equal("Updated details", (await ReadAsync(changeDescription)).Description);
    }

    [Fact]
    public async Task HostCanCompleteWithoutAutomaticTimeTransition_AndOtherUsersCannotCancel()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var other = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "intermediate");
        var request = await CreateRequestAsync(client, host.AccessToken, ValidBody());

        using var forbiddenCancel = await PostAuthorizedAsync(client, other.AccessToken, $"{BasePath}/{request.Id}/cancel");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenCancel.StatusCode);
        using var complete = await PostAuthorizedAsync(client, host.AccessToken, $"{BasePath}/{request.Id}/complete");
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("completed", (await ReadAsync(complete)).Status);
        using var discovery = await GetAuthorizedAsync(client, host.AccessToken, BasePath);
        Assert.DoesNotContain((await ReadPageAsync(discovery)).Items, item => item.Id == request.Id);
        using var mine = await GetAuthorizedAsync(client, host.AccessToken, $"{BasePath}/me");
        Assert.Contains((await ReadPageAsync(mine)).Items, item => item.Id == request.Id);
    }

    [Fact]
    public async Task Discovery_AppliesRadiusTimeAndPaginationFilters()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var viewer = await AuthApi.RegisterAndLoginAsync(client);
        await SetSportProfileAsync(client, host.AccessToken, Tennis, "intermediate");
        await SetSportProfileAsync(client, viewer.AccessToken, Tennis, "intermediate");

        var firstBody = ValidBody();
        firstBody["latitude"] = 21.028511m;
        firstBody["longitude"] = 105.804817m;
        var first = await CreateRequestAsync(client, host.AccessToken, firstBody);
        var secondBody = ValidBody();
        secondBody["latitude"] = 10.823099m;
        secondBody["longitude"] = 106.629662m;
        var second = await CreateRequestAsync(client, host.AccessToken, secondBody);

        var filter = $"?sport_id={Tennis}&starts_from_utc={Uri.EscapeDataString(first.StartsAtUtc.AddMinutes(-1).ToString("O"))}"
            + $"&starts_to_utc={Uri.EscapeDataString(first.StartsAtUtc.AddMinutes(1).ToString("O"))}"
            + "&latitude=21.028511&longitude=105.804817&radius_km=5&page=1&page_size=1";
        using var response = await GetAuthorizedAsync(client, viewer.AccessToken, BasePath + filter);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadPageAsync(response);
        Assert.Single(page.Items);
        Assert.Equal(first.Id, page.Items[0].Id);
        Assert.Equal(1, page.TotalCount);
        Assert.NotEqual(first.Id, second.Id);
    }

    private static Dictionary<string, object?> ValidBody(
        string? min = "beginner",
        string? max = null,
        byte spots = 2)
    {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(10), TimeSpan.Zero);
        return new Dictionary<string, object?>
        {
            ["sport_id"] = Tennis,
            ["title"] = "Tennis after work",
            ["description"] = "Looking for players",
            ["starts_at"] = start.ToString("O"),
            ["ends_at"] = start.AddHours(2).ToString("O"),
            ["spots_needed"] = spots,
            ["latitude"] = 21.028511m,
            ["longitude"] = 105.804817m,
            ["min_skill_level"] = min,
            ["max_skill_level"] = max,
            ["location_name"] = "Ba Dinh"
        };
    }

    private static Dictionary<string, object?> UpdateBody(
        PlayRequestDto request,
        string? title = null,
        string? description = null) => new()
    {
        ["sport_id"] = request.SportId,
        ["title"] = title ?? request.Title,
        ["description"] = description ?? request.Description,
        ["starts_at"] = request.StartsAtUtc,
        ["ends_at"] = request.EndsAtUtc,
        ["spots_needed"] = request.SpotsNeeded,
        ["latitude"] = request.Latitude ?? 21.028511m,
        ["longitude"] = request.Longitude ?? 105.804817m,
        ["min_skill_level"] = request.MinSkillLevel,
        ["max_skill_level"] = request.MaxSkillLevel,
        ["location_name"] = request.LocationName
    };

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Post, BasePath, token, body));

    private static async Task<PlayRequestDto> CreateRequestAsync(
        HttpClient client,
        string token,
        object? body = null)
    {
        using var response = await CreateAsync(client, token, body ?? ValidBody());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task SetSportProfileAsync(HttpClient client, string token, Guid sportId, string level)
    {
        using var response = await client.SendAsync(AuthApi.Authorized(
            HttpMethod.Put, $"{MySports}/{sportId}", token, new { skill_level = level }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> GetAuthorizedAsync(HttpClient client, string token, string path) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Get, path, token));

    private static Task<HttpResponseMessage> PostAuthorizedAsync(HttpClient client, string token, string path) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Post, path, token));

    private static Task<HttpResponseMessage> PutAuthorizedAsync(HttpClient client, string token, string path, object body) =>
        client.SendAsync(AuthApi.Authorized(HttpMethod.Put, path, token, body));

    private static async Task JoinAsync(HttpClient client, string token, Guid requestId)
    {
        using var response = await PostAuthorizedAsync(client, token, $"{BasePath}/{requestId}/participants");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<ParticipantDto>> ReadParticipantsAsync(HttpClient client, string token, Guid requestId)
    {
        using var response = await GetAuthorizedAsync(client, token, $"{BasePath}/{requestId}/participants");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ParticipantDto>>(AuthApi.Json))!;
    }

    private static async Task<PlayRequestDto> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PlayRequestDto>(AuthApi.Json))!;

    private static async Task<PlayRequestPageDto> ReadPageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PlayRequestPageDto>(AuthApi.Json))!;

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    private static async Task EnsureStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
        }

        await Task.CompletedTask;
    }

    private sealed record PlayRequestPageDto(List<PlayRequestDto> Items, int Page, int PageSize, int TotalCount);

    private sealed record HostDto(string DisplayName, string? SkillLevel);

    private sealed record ParticipantDto(
        Guid Id,
        string DisplayName,
        string? SkillLevel,
        string Status,
        DateTimeOffset RequestedAtUtc);

    private sealed record PlayRequestDto(
        Guid Id,
        Guid SportId,
        string SportName,
        string SportSlug,
        string Title,
        string? Description,
        DateTimeOffset StartsAtUtc,
        DateTimeOffset EndsAtUtc,
        byte SpotsNeeded,
        int AcceptedCount,
        string? LocationName,
        string? MinSkillLevel,
        string? MaxSkillLevel,
        string Status,
        HostDto Host,
        string? MyParticipantStatus,
        List<ParticipantDto> Participants,
        decimal? Latitude,
        decimal? Longitude,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);
}
