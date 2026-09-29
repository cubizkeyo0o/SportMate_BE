using System.Net;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;
using static SportMate.IntegrationTests.Infrastructure.BookingApi;

namespace SportMate.IntegrationTests.Bookings;

/// <summary>
/// Real MySQL/InnoDB: concurrent booking requests for one physical court are serialized by the
/// <c>SELECT ... FOR UPDATE</c> lock on the court row. Skipped unless SPORTMATE_TEST_MYSQL is set.
/// </summary>
public sealed class MySqlBookingConcurrencyTests(MySqlApiFactory factory) : IClassFixture<MySqlApiFactory>
{
    private const int ParallelRequests = 8;

    // All of these contain 10:30-10:45, so every pair overlaps.
    private static readonly (string Start, string End)[] OverlappingSlots =
        [("10:00", "11:00"), ("10:15", "11:15"), ("10:30", "11:30"), ("09:45", "10:45")];

    [MySqlFact]
    public async Task ConcurrentOverlappingRequests_ExactlyOneSucceeds_OthersGet409()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var tokens = await LoginUsersAsync(client, ParallelRequests);

        // Several rounds, each on its own day, so a lucky interleaving cannot hide a race.
        for (var round = 0; round < 5; round++)
        {
            var day = LocalDate(daysAhead: 7 + round);
            using var start = new SemaphoreSlim(0);
            var requests = tokens.Select((token, i) => Task.Run(async () =>
            {
                await start.WaitAsync();
                var (slotStart, slotEnd) = OverlappingSlots[i % OverlappingSlots.Length];
                // Alternate sports: the physical court is the conflict unit.
                var courtSport = i % 2 == 0 ? court.TennisCourtSportId : court.BadmintonCourtSportId;
                using var response = await CreateAsync(client, token, Body(courtSport, VietnamTime(day, slotStart), VietnamTime(day, slotEnd)));
                return response.StatusCode;
            })).ToList();

            start.Release(ParallelRequests);
            var statuses = await Task.WhenAll(requests);

            Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(ParallelRequests - 1, statuses.Count(s => s == HttpStatusCode.Conflict));
        }

        await factory.WithDbAsync(async db =>
        {
            var bookings = await db.Bookings.AsNoTracking().Where(b => b.CourtId == court.Court.Id).ToListAsync();
            Assert.Equal(5, bookings.Count);
            Assert.All(bookings, b => Assert.Equal(BookingStatus.Pending, b.Status));
        });
    }

    [MySqlFact]
    public async Task ConcurrentRequestsForDifferentCourts_AllSucceed()
    {
        using var client = factory.CreateClient();
        var courts = new List<ArrangedCourt>();
        for (var i = 0; i < 4; i++)
        {
            courts.Add(await ArrangeCourtAsync(factory, client));
        }

        var tokens = await LoginUsersAsync(client, courts.Count);
        var day = LocalDate(daysAhead: 20);

        var statuses = await Task.WhenAll(courts.Select((court, i) => Task.Run(async () =>
        {
            using var response = await CreateAsync(client, tokens[i],
                Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));
            return response.StatusCode;
        })));

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Created, s));
    }

    [MySqlFact]
    public async Task BookingWaitsForTheCourtRowLock()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var day = LocalDate(daysAhead: 25);

        // Another transaction holds the court row, as a concurrent booking would.
        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using (var lockCommand = new MySqlCommand("SELECT id FROM courts WHERE id = @id FOR UPDATE", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("@id", court.Court.Id.ToString());
            await lockCommand.ExecuteScalarAsync();
        }

        var pending = CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));

        var finishedFirst = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(pending, finishedFirst); // blocked on the lock

        await transaction.CommitAsync();
        using var response = await pending;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [MySqlFact]
    public async Task ExpiredHold_IsCancelledAndFreesTheCourt_OnMySql()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var tokens = await LoginUsersAsync(client, 2);
        var day = LocalDate(daysAhead: 30);
        var first = await CreateBookingAsync(client, tokens[0],
            Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));
        await factory.SetBookingTimesAsync(first.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-16));

        var second = await CreateBookingAsync(client, tokens[1],
            Body(court.BadmintonCourtSportId, VietnamTime(day, "10:30"), VietnamTime(day, "11:30")));

        using var pay = await ActionAsync(client, tokens[1], second.Id, "pay");
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var expired = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == first.Id);
            Assert.Equal(BookingStatus.Cancelled, expired.Status);
            Assert.Equal(Booking.HoldExpiredReason, expired.CancellationReason);
        });
    }

    private static async Task<List<string>> LoginUsersAsync(HttpClient client, int count)
    {
        var tokens = new List<string>();
        for (var i = 0; i < count; i++)
        {
            tokens.Add((await AuthApi.RegisterAndLoginAsync(client)).AccessToken);
        }

        return tokens;
    }
}
