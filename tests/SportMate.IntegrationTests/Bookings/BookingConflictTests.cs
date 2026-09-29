using System.Net;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Infrastructure;
using static SportMate.IntegrationTests.Infrastructure.BookingApi;

namespace SportMate.IntegrationTests.Bookings;

/// <summary>
/// Conflicts are per physical court: Pending (while its hold runs) and Confirmed bookings block it.
/// These run on SQLite; the concurrent path is covered against MySQL in <see cref="MySqlBookingConcurrencyTests"/>.
/// </summary>
public sealed class BookingConflictTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private static readonly DateOnly Day = LocalDate();

    [Theory]
    [InlineData("10:00", "11:00")] // same time
    [InlineData("09:30", "10:30")] // overlaps the beginning
    [InlineData("10:30", "11:30")] // overlaps the end
    [InlineData("09:00", "12:00")] // contains the existing booking
    [InlineData("10:15", "10:45")] // inside the existing booking
    [InlineData("10:59", "12:00")] // one minute of overlap
    public async Task Overlap_WithPendingBooking_Returns409(string start, string end)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");

        using var response = await TryBookAsync(client, court.TennisCourtSportId, start, end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertBookingCountAsync(court.Court.Id, 1);
    }

    [Theory]
    [InlineData("09:00", "10:00")] // ends when the existing one starts
    [InlineData("11:00", "12:00")] // starts when the existing one ends
    public async Task TouchingBookings_DoNotConflict(string start, string end)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");

        using var response = await TryBookAsync(client, court.TennisCourtSportId, start, end);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DifferentSportsOnTheSameCourt_StillConflict()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");

        using var response = await TryBookAsync(client, court.BadmintonCourtSportId, "10:30", "11:30");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DifferentCourts_CanBeBookedForTheSameTime()
    {
        using var client = factory.CreateClient();
        var first = await ArrangeCourtAsync(factory, client);
        var second = await ArrangeCourtAsync(factory, client);
        await BookAsync(client, first.TennisCourtSportId, "10:00", "11:00");

        using var response = await TryBookAsync(client, second.TennisCourtSportId, "10:00", "11:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmedBooking_Blocks()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var (booking, token) = await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");
        using (var pay = await ActionAsync(client, token, booking.Id, "pay"))
        {
            Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        }

        // Still blocking long after the hold would have expired.
        await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: DateTime.UtcNow.AddHours(-2));

        using var response = await TryBookAsync(client, court.BadmintonCourtSportId, "10:30", "11:30");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CancelledBooking_DoesNotBlock()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var (booking, token) = await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");
        using (await ActionAsync(client, token, booking.Id, "cancel"))
        {
        }

        using var response = await TryBookAsync(client, court.TennisCourtSportId, "10:00", "11:00");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.NoShow)]
    public async Task CompletedOrNoShowBooking_DoesNotBlock(BookingStatus status)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var (booking, token) = await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");
        using (await ActionAsync(client, token, booking.Id, "pay"))
        {
        }

        // Move the booking into the past so it can be closed, then back onto the slot being requested.
        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: DateTime.UtcNow.AddHours(-3), endsAtUtc: DateTime.UtcNow.AddHours(-2));
        using (var close = await ActionAsync(client, court.Owner.AccessToken, booking.Id, status == BookingStatus.Completed ? "complete" : "no-show"))
        {
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        }

        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: booking.StartsAtUtc, endsAtUtc: booking.EndsAtUtc);

        using var response = await TryBookAsync(client, court.TennisCourtSportId, "10:00", "11:00");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ActivePendingHold_Blocks_UntilItExpires()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var (booking, _) = await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");

        // 14 minutes into the hold: still blocking.
        await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-14));
        using (var blocked = await TryBookAsync(client, court.TennisCourtSportId, "10:00", "11:00"))
        {
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }

        // Past 15 minutes: the hold is cancelled and the slot is free again.
        await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-16));
        using var response = await TryBookAsync(client, court.BadmintonCourtSportId, "10:00", "11:00");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var expired = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == booking.Id);
            Assert.Equal(BookingStatus.Cancelled, expired.Status);
            Assert.Equal(Booking.HoldExpiredReason, expired.CancellationReason);
            Assert.NotNull(expired.CancelledAtUtc);
        });
    }

    [Fact]
    public async Task ExpiredHold_IsShownAsCancelledWhenRead()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var (booking, token) = await BookAsync(client, court.TennisCourtSportId, "10:00", "11:00");
        await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-20));

        using var get = await GetAsync(client, token, PathOf(booking.Id));
        var single = await ReadAsync(get);
        using var mine = await GetAsync(client, token, $"{BasePath}/me");
        using var courtList = await GetAsync(client, court.Owner.AccessToken, $"/api/v1/courts/{court.Court.Id}/bookings");

        Assert.Equal("cancelled", single.Status);
        Assert.Equal("Hold expired", single.CancellationReason);
        Assert.Null(single.HoldExpiresAtUtc);
        Assert.Equal("cancelled", Assert.Single(await ReadListAsync(mine)).Status);
        Assert.Equal("cancelled", Assert.Single(await ReadListAsync(courtList)).Status);
    }

    [Fact]
    public async Task ExpiringHoldsOnOneCourt_DoesNotTouchOtherCourtsOrConfirmedBookings()
    {
        using var client = factory.CreateClient();
        var first = await ArrangeCourtAsync(factory, client);
        var second = await ArrangeCourtAsync(factory, client);
        var (otherCourtHold, _) = await BookAsync(client, second.TennisCourtSportId, "10:00", "11:00");
        var (confirmed, token) = await BookAsync(client, first.TennisCourtSportId, "12:00", "13:00");
        using (await ActionAsync(client, token, confirmed.Id, "pay"))
        {
        }

        await factory.SetBookingTimesAsync(otherCourtHold.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-30));
        await factory.SetBookingTimesAsync(confirmed.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-30));

        await BookAsync(client, first.TennisCourtSportId, "10:00", "11:00");

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(BookingStatus.Pending, (await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == otherCourtHold.Id)).Status);
            Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == confirmed.Id)).Status);
        });
    }

    private async Task<(BookingDto Booking, string Token)> BookAsync(HttpClient client, Guid courtSportId, string start, string end)
    {
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(courtSportId, VietnamTime(Day, start), VietnamTime(Day, end)));
        return (booking, user.AccessToken);
    }

    private static async Task<HttpResponseMessage> TryBookAsync(HttpClient client, Guid courtSportId, string start, string end)
    {
        var user = await AuthApi.RegisterAndLoginAsync(client);
        return await CreateAsync(client, user.AccessToken, Body(courtSportId, VietnamTime(Day, start), VietnamTime(Day, end)));
    }

    private Task AssertBookingCountAsync(Guid courtId, int expected) => factory.WithDbAsync(async db =>
        Assert.Equal(expected, await db.Bookings.CountAsync(b => b.CourtId == courtId)));
}
