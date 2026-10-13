using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;

namespace SportMate.IntegrationTests.PlayRequests;

/// <summary>
/// Verifies InnoDB serializes concurrent host acceptances so capacity cannot be exceeded.
/// Skipped unless SPORTMATE_TEST_MYSQL is set.
/// </summary>
public sealed class MySqlPlayRequestConcurrencyTests(MySqlApiFactory factory)
    : IClassFixture<MySqlApiFactory>
{
    private const string BasePath = "/api/v1/play-requests";

    [MySqlFact]
    public async Task ConcurrentAcceptances_DoNotExceedSpotsNeeded()
    {
        using var client = factory.CreateClient();
        var host = await AuthApi.RegisterAndLoginAsync(client);
        var firstPlayer = await AuthApi.RegisterAndLoginAsync(client);
        var secondPlayer = await AuthApi.RegisterAndLoginAsync(client);
        var sport = await factory.AddSportAsync();

        await AddSportProfileAsync(host.User.Id, sport.Id);
        await AddSportProfileAsync(firstPlayer.User.Id, sport.Id);
        await AddSportProfileAsync(secondPlayer.User.Id, sport.Id);

        var startsAt = RoundToMinute(DateTimeOffset.UtcNow.AddDays(2));
        var body = new
        {
            sport_id = sport.Id,
            title = "Concurrent acceptance test",
            description = (string?)null,
            starts_at = startsAt.ToString("O"),
            ends_at = startsAt.AddHours(1).ToString("O"),
            spots_needed = 1,
            latitude = 21.028511m,
            longitude = 105.804817m,
            min_skill_level = (string?)null,
            max_skill_level = (string?)null,
            location_name = (string?)null
        };

        using var create = await SendAsync(client, HttpMethod.Post, BasePath, host.AccessToken, body);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var requestId = createdJson.RootElement.GetProperty("id").GetGuid();

        foreach (var player in new[] { firstPlayer, secondPlayer })
        {
            using var join = await SendAsync(
                client, HttpMethod.Post, $"{BasePath}/{requestId}/participants", player.AccessToken);
            Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        }

        using var participantResponse = await SendAsync(
            client, HttpMethod.Get, $"{BasePath}/{requestId}/participants", host.AccessToken);
        Assert.Equal(HttpStatusCode.OK, participantResponse.StatusCode);
        using var participantsJson = JsonDocument.Parse(await participantResponse.Content.ReadAsStringAsync());
        var participantIds = participantsJson.RootElement.EnumerateArray()
            .Select(participant => participant.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Equal(2, participantIds.Length);

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptances = participantIds.Select(async participantId =>
        {
            await gate.Task;
            using var response = await SendAsync(
                client,
                HttpMethod.Post,
                $"{BasePath}/{requestId}/participants/{participantId}/accept",
                host.AccessToken);
            return response.StatusCode;
        }).ToArray();

        gate.SetResult(true);
        var statuses = await Task.WhenAll(acceptances);

        Assert.Single(statuses, status => status == HttpStatusCode.OK);
        Assert.Single(statuses, status => status == HttpStatusCode.Conflict);
        await factory.WithDbAsync(async db =>
        {
            var request = await db.PlayRequests.AsNoTracking().SingleAsync(item => item.Id == requestId);
            var participants = await db.PlayRequestParticipants.AsNoTracking()
                .Where(participant => participant.PlayRequestId == requestId)
                .ToListAsync();

            Assert.Equal(PlayRequestStatus.Full, request.Status);
            Assert.Single(participants, participant => participant.Status == ParticipantStatus.Accepted);
            Assert.Single(participants, participant => participant.Status == ParticipantStatus.Pending);
        });
    }

    private Task AddSportProfileAsync(Guid userId, Guid sportId) => factory.WithDbAsync(async db =>
    {
        db.UserSportProfiles.Add(new UserSportProfile(
            userId, sportId, SkillLevel.Intermediate, DateTime.UtcNow));
        await db.SaveChangesAsync();
    });

    private static DateTimeOffset RoundToMinute(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null) =>
        client.SendAsync(AuthApi.Authorized(method, path, accessToken, body));
}
