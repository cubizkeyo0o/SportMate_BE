using System.Net;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;
using Xunit.Abstractions;
using static SportMate.IntegrationTests.Infrastructure.BookingApi;

namespace SportMate.IntegrationTests.Bookings;

/// <summary>
/// Real MySQL/InnoDB: concurrent booking requests for one physical court are serialized by the
/// <c>SELECT ... FOR UPDATE</c> lock on the court row. Skipped unless SPORTMATE_TEST_MYSQL is set.
/// </summary>
public sealed class MySqlBookingConcurrencyTests(MySqlApiFactory factory, ITestOutputHelper output)
    : IClassFixture<MySqlApiFactory>
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

    /// <summary>
    /// Pay races against hold expiry (triggered by concurrent reads) right at the moment the hold runs out.
    /// Exactly one transition out of Pending may win, and an expired hold is never confirmed.
    /// </summary>
    [MySqlFact]
    public async Task PayRacingHoldExpiry_ExactlyOneTransitionWins()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var hold = TimeSpan.FromMinutes(BookingOptionsHold);
        int confirmedCount = 0, expiredCount = 0;

        for (var i = 0; i < 20; i++)
        {
            var day = LocalDate(daysAhead: 40 + i);
            var booking = await CreateBookingAsync(client, user.AccessToken,
                Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));

            // Make the hold run out a few milliseconds from now (spread across iterations).
            var createdAt = DateTime.UtcNow - hold + TimeSpan.FromMilliseconds(i * 10);
            await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: createdAt);

            using var go = new SemaphoreSlim(0);
            var pay = Task.Run(async () =>
            {
                await go.WaitAsync();
                using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");
                return response.StatusCode;
            });
            var reads = new[]
            {
                PathOf(booking.Id), $"{BasePath}/me", PathOf(booking.Id)
            }.Select(path => Task.Run(async () =>
            {
                await go.WaitAsync();
                using var response = await GetAsync(client, user.AccessToken, path);
                return response.StatusCode;
            })).Append(Task.Run(async () =>
            {
                await go.WaitAsync();
                using var response = await GetAsync(client, court.Owner.AccessToken, $"/api/v1/courts/{court.Court.Id}/bookings");
                return response.StatusCode;
            })).ToList();

            go.Release(5);
            var payStatus = await pay;
            Assert.All(await Task.WhenAll(reads), s => Assert.Equal(HttpStatusCode.OK, s));

            var stored = await StoredAsync(booking.Id);
            if (stored.Status == BookingStatus.Confirmed)
            {
                confirmedCount++;
                Assert.Equal(HttpStatusCode.OK, payStatus);
                Assert.Null(stored.CancellationReason);
                Assert.Null(stored.CancelledAtUtc);
                Assert.True(stored.UpdatedAtUtc < stored.CreatedAtUtc + hold, "confirmed after the hold expired");
            }
            else
            {
                expiredCount++;
                Assert.Equal(BookingStatus.Cancelled, stored.Status);
                Assert.Equal(HttpStatusCode.BadRequest, payStatus);
                Assert.Equal(Booking.HoldExpiredReason, stored.CancellationReason);
                Assert.True(stored.CancelledAtUtc >= stored.CreatedAtUtc + hold, "cancelled before the hold expired");
            }
        }

        Assert.Equal(20, confirmedCount + expiredCount);
        output.WriteLine($"pay won {confirmedCount} times, hold expiry won {expiredCount} times");
    }

    /// <summary>
    /// Pay and cancel of the same Pending booking at the same time: the outcome is always one of the two
    /// serial orders, never a mix (for example Confirmed with a cancellation reason).
    /// </summary>
    [MySqlFact]
    public async Task PayRacingCancel_OutcomeMatchesOneSerialOrder()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        for (var i = 0; i < 10; i++)
        {
            var day = LocalDate(daysAhead: 70 + i);
            var booking = await CreateBookingAsync(client, user.AccessToken,
                Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));

            using var go = new SemaphoreSlim(0);
            var pay = Task.Run(async () =>
            {
                await go.WaitAsync();
                using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");
                return response.StatusCode;
            });
            var cancel = Task.Run(async () =>
            {
                await go.WaitAsync();
                using var response = await ActionAsync(client, court.Owner.AccessToken, booking.Id, "cancel", new { reason = "race" });
                return response.StatusCode;
            });
            go.Release(2);
            var (payStatus, cancelStatus) = (await pay, await cancel);

            // Cancel is valid from Pending and from Confirmed, so it always wins last; pay succeeds only if it ran first.
            Assert.Equal(HttpStatusCode.OK, cancelStatus);
            Assert.Contains(payStatus, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
            var stored = await StoredAsync(booking.Id);
            Assert.Equal(BookingStatus.Cancelled, stored.Status);
            Assert.Equal("race", stored.CancellationReason);
            Assert.NotNull(stored.CancelledAtUtc);
        }
    }

    /// <summary>
    /// Pay and the read-path hold expiry both wait for the court row lock, like booking creation.
    /// </summary>
    [MySqlFact]
    public async Task PayAndHoldExpiry_WaitForTheCourtRowLock()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var day = LocalDate(daysAhead: 90);
        var expiring = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(day, "08:00"), VietnamTime(day, "09:00")));
        var toPay = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(day, "10:00"), VietnamTime(day, "11:00")));
        await factory.SetBookingTimesAsync(expiring.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-20));

        await using var connection = new MySqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await using (var lockCommand = new MySqlCommand("SELECT id FROM courts WHERE id = @id FOR UPDATE", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("@id", court.Court.Id.ToString());
            await lockCommand.ExecuteScalarAsync();
        }

        var pay = ActionAsync(client, user.AccessToken, toPay.Id, "pay");
        var read = GetAsync(client, user.AccessToken, PathOf(expiring.Id));
        var finishedFirst = await Task.WhenAny(Task.WhenAny(pay, read), Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.False(pay.IsCompleted, "pay did not wait for the court lock");
        Assert.False(read.IsCompleted, "hold expiry did not wait for the court lock");

        await transaction.CommitAsync();
        using var payResponse = await pay;
        using var readResponse = await read;
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);
        Assert.Equal("confirmed", (await ReadAsync(payResponse)).Status);
        Assert.Equal("cancelled", (await ReadAsync(readResponse)).Status);
    }

    private const int BookingOptionsHold = SportMate.Application.Bookings.BookingOptions.DefaultHoldDurationMinutes;

    private async Task<Booking> StoredAsync(Guid bookingId)
    {
        Booking? booking = null;
        await factory.WithDbAsync(async db => booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId));
        return booking!;
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
