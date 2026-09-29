using System.Net;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Courts;
using SportMate.IntegrationTests.Infrastructure;
using static SportMate.IntegrationTests.Infrastructure.BookingApi;

namespace SportMate.IntegrationTests.Bookings;

public sealed class BookingLifecycleTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private static readonly DateOnly Day = LocalDate();

    // ---- mock payment ----

    [Fact]
    public async Task Pay_ByBookerWithinHold_ConfirmsTheBooking()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = await ReadAsync(response);
        Assert.Equal("confirmed", paid.Status);
        Assert.Null(paid.HoldExpiresAtUtc);
        Assert.Equal(booking.TotalPrice, paid.TotalPrice);
        Assert.Equal(booking.CreatedAtUtc, paid.CreatedAtUtc);
        Assert.True(paid.UpdatedAtUtc >= booking.UpdatedAtUtc);
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Pay_AfterHoldExpired_Returns400AndCancelsTheBooking()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        await factory.SetBookingTimesAsync(booking.Id, createdAtUtc: DateTime.UtcNow.AddMinutes(-15).AddSeconds(-1));

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
        var stored = await StoredAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, stored.Status);
        Assert.Equal(Booking.HoldExpiredReason, stored.CancellationReason);
        Assert.NotNull(stored.CancelledAtUtc);

        // Paying again is still rejected; the expired hold is not revived.
        using var again = await ActionAsync(client, user.AccessToken, booking.Id, "pay");
        await CreateCourtTests.AssertValidationErrorAsync(again, "status");
    }

    [Fact]
    public async Task Pay_AlreadyConfirmed_Returns400()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "pay"))
        {
        }

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
    }

    [Fact]
    public async Task Pay_CancelledBooking_Returns400()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "cancel"))
        {
        }

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
        Assert.Equal(BookingStatus.Cancelled, (await StoredAsync(booking.Id)).Status);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("no-show")]
    public async Task Pay_CompletedOrNoShowBooking_Returns400(string closingAction)
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        await PayAndEndAsync(client, user, booking);
        using (var close = await ActionAsync(client, court.Owner.AccessToken, booking.Id, closingAction))
        {
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        }

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "pay");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
    }

    [Fact]
    public async Task Pay_ByCourtOwnerOrAdmin_Returns403_AndByUnrelatedUser_Returns404()
    {
        using var client = factory.CreateClient();
        var (court, _, booking) = await ArrangeBookingAsync(client);
        var stranger = await AuthApi.RegisterAndLoginAsync(client);

        using var owner = await ActionAsync(client, court.Owner.AccessToken, booking.Id, "pay");
        using var admin = await ActionAsync(client, court.Admin.AccessToken, booking.Id, "pay");
        using var unrelated = await ActionAsync(client, stranger.AccessToken, booking.Id, "pay");

        Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        Assert.Equal(BookingStatus.Pending, (await StoredAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task NoRealPaymentInfrastructureExists()
    {
        await factory.WithDbAsync(db =>
        {
            var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()).ToList();
            Assert.DoesNotContain(tables, t => t!.Contains("payment", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(tables, t => t!.Contains("hold", StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        });
    }

    // ---- cancel ----

    [Theory]
    [InlineData("booker")]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task Cancel_ByBookerCourtOwnerOrAdmin_CancelsWithReason(string who)
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        var token = who switch { "booker" => user.AccessToken, "owner" => court.Owner.AccessToken, _ => court.Admin.AccessToken };
        var before = DateTime.UtcNow;

        using var response = await ActionAsync(client, token, booking.Id, "cancel", new { reason = "  Court maintenance  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await ReadAsync(response);
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal("Court maintenance", cancelled.CancellationReason);
        Assert.InRange(cancelled.CancelledAtUtc!.Value, before, DateTime.UtcNow);

        var stored = await StoredAsync(booking.Id); // still there: never hard-deleted
        Assert.Equal(BookingStatus.Cancelled, stored.Status);
        Assert.Equal("Court maintenance", stored.CancellationReason);
    }

    [Fact]
    public async Task Cancel_ConfirmedBooking_WithoutBody_Succeeds()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "pay"))
        {
        }

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await ReadAsync(response);
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Null(cancelled.CancellationReason);
    }

    [Fact]
    public async Task Cancel_ByUnrelatedUserOrOtherCourtOwner_Returns404()
    {
        using var client = factory.CreateClient();
        var (_, _, booking) = await ArrangeBookingAsync(client);
        var stranger = await AuthApi.RegisterAndLoginAsync(client);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);

        using var byStranger = await ActionAsync(client, stranger.AccessToken, booking.Id, "cancel");
        using var byOtherOwner = await ActionAsync(client, otherOwner.AccessToken, booking.Id, "cancel");

        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byOtherOwner.StatusCode);
        Assert.Equal(BookingStatus.Pending, (await StoredAsync(booking.Id)).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public async Task Cancel_AtOrAfterStart_Returns400(int minutesSinceStart)
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "pay"))
        {
        }

        var start = DateTime.UtcNow.AddMinutes(-minutesSinceStart).AddSeconds(-1);
        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: start, endsAtUtc: start.AddHours(2));

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "cancel");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Cancel_WithTooLongReason_Returns400()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "cancel", new { reason = new string('r', 501) });

        await CreateCourtTests.AssertValidationErrorAsync(response, "reason");
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Returns400()
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "cancel"))
        {
        }

        using var response = await ActionAsync(client, user.AccessToken, booking.Id, "cancel");

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
    }

    // ---- complete / no-show ----

    [Theory]
    [InlineData("complete", "completed", "owner")]
    [InlineData("complete", "completed", "admin")]
    [InlineData("no-show", "no_show", "owner")]
    [InlineData("no-show", "no_show", "admin")]
    public async Task CloseAfterEnd_ByCourtOwnerOrAdmin_Succeeds(string action, string status, string who)
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        await PayAndEndAsync(client, user, booking);
        var token = who == "owner" ? court.Owner.AccessToken : court.Admin.AccessToken;

        using var response = await ActionAsync(client, token, booking.Id, action);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, (await ReadAsync(response)).Status);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("no-show")]
    public async Task Close_ByBooker_Returns403_AndByUnrelatedUser_Returns404(string action)
    {
        using var client = factory.CreateClient();
        var (_, user, booking) = await ArrangeBookingAsync(client);
        await PayAndEndAsync(client, user, booking);
        var stranger = await AuthApi.RegisterAndLoginAsync(client);

        using var byBooker = await ActionAsync(client, user.AccessToken, booking.Id, action);
        using var byStranger = await ActionAsync(client, stranger.AccessToken, booking.Id, action);

        Assert.Equal(HttpStatusCode.Forbidden, byBooker.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Id)).Status);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("no-show")]
    public async Task Close_BeforeEnd_Returns400(string action)
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        using (await ActionAsync(client, user.AccessToken, booking.Id, "pay"))
        {
        }

        // Started but not yet ended.
        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: DateTime.UtcNow.AddMinutes(-30), endsAtUtc: DateTime.UtcNow.AddMinutes(30));

        using var response = await ActionAsync(client, court.Owner.AccessToken, booking.Id, action);

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
        Assert.Equal(BookingStatus.Confirmed, (await StoredAsync(booking.Id)).Status);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("no-show")]
    public async Task Close_PendingBooking_Returns400(string action)
    {
        using var client = factory.CreateClient();
        var (court, _, booking) = await ArrangeBookingAsync(client);
        // Ended but still inside its (fresh) hold, so it stays Pending.
        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: DateTime.UtcNow.AddHours(-2), endsAtUtc: DateTime.UtcNow.AddHours(-1));

        using var response = await ActionAsync(client, court.Admin.AccessToken, booking.Id, action);

        await CreateCourtTests.AssertValidationErrorAsync(response, "status");
        Assert.Equal(BookingStatus.Pending, (await StoredAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task ClosedBookings_CannotChangeAgain()
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        await PayAndEndAsync(client, user, booking);
        using (await ActionAsync(client, court.Owner.AccessToken, booking.Id, "complete"))
        {
        }

        using var noShow = await ActionAsync(client, court.Owner.AccessToken, booking.Id, "no-show");
        using var complete = await ActionAsync(client, court.Owner.AccessToken, booking.Id, "complete");
        using var cancel = await ActionAsync(client, court.Owner.AccessToken, booking.Id, "cancel");

        await CreateCourtTests.AssertValidationErrorAsync(noShow, "status");
        await CreateCourtTests.AssertValidationErrorAsync(complete, "status");
        await CreateCourtTests.AssertValidationErrorAsync(cancel, "status");
        Assert.Equal(BookingStatus.Completed, (await StoredAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Actions_OnUnknownBooking_Return404()
    {
        using var client = factory.CreateClient();
        var admin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var missing = Guid.NewGuid();

        foreach (var action in new[] { "pay", "cancel", "complete", "no-show" })
        {
            using var response = await ActionAsync(client, admin.AccessToken, missing, action);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var get = await GetAsync(client, admin.AccessToken, PathOf(missing));
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();
        var (court, _, booking) = await ArrangeBookingAsync(client);

        using var me = await client.GetAsync($"{BasePath}/me");
        using var get = await client.GetAsync(PathOf(booking.Id));
        using var pay = await client.PostAsync($"{PathOf(booking.Id)}/pay", null);
        using var cancel = await client.PostAsync($"{PathOf(booking.Id)}/cancel", null);
        using var courtList = await client.GetAsync($"/api/v1/courts/{court.Court.Id}/bookings");

        Assert.All([me, get, pay, cancel, courtList], r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    // ---- visibility ----

    [Fact]
    public async Task Get_IsVisibleToBookerCourtOwnerAndAdmin_Only()
    {
        using var client = factory.CreateClient();
        var (court, user, booking) = await ArrangeBookingAsync(client);
        var otherAdmin = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.Admin);
        var otherOwner = await CourtApi.LoginWithRoleAsync(factory, client, UserRole.CourtOwner);
        var stranger = await AuthApi.RegisterAndLoginAsync(client);

        foreach (var token in new[] { user.AccessToken, court.Owner.AccessToken, otherAdmin.AccessToken })
        {
            using var visible = await GetAsync(client, token, PathOf(booking.Id));
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Equal(booking.Id, (await ReadAsync(visible)).Id);
        }

        foreach (var token in new[] { stranger.AccessToken, otherOwner.AccessToken })
        {
            using var hidden = await GetAsync(client, token, PathOf(booking.Id));
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
    }

    [Fact]
    public async Task ListMine_ReturnsOnlyTheCallersBookingsOrderedByStart()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var alice = await AuthApi.RegisterAndLoginAsync(client);
        var bob = await AuthApi.RegisterAndLoginAsync(client);
        var late = await CreateBookingAsync(client, alice.AccessToken, Body(court.TennisCourtSportId, VietnamTime(Day, "16:00"), VietnamTime(Day, "17:00")));
        var early = await CreateBookingAsync(client, alice.AccessToken, Body(court.TennisCourtSportId, VietnamTime(Day, "08:00"), VietnamTime(Day, "09:00")));
        await CreateBookingAsync(client, bob.AccessToken, Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        using var response = await GetAsync(client, alice.AccessToken, $"{BasePath}/me");

        Assert.Equal([early.Id, late.Id], (await ReadListAsync(response)).Select(b => b.Id));

        var fresh = await AuthApi.RegisterAndLoginAsync(client);
        using var empty = await GetAsync(client, fresh.AccessToken, $"{BasePath}/me");
        Assert.Empty(await ReadListAsync(empty));
    }

    [Fact]
    public async Task CourtBookings_ForAdminAndAssignedOwner_ButNotOthers()
    {
        using var client = factory.CreateClient();
        var (court, _, booking) = await ArrangeBookingAsync(client);
        var otherCourt = await ArrangeCourtAsync(factory, client);
        var otherUser = await AuthApi.RegisterAndLoginAsync(client);
        await CreateBookingAsync(client, otherUser.AccessToken, Body(otherCourt.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));
        var path = $"/api/v1/courts/{court.Court.Id}/bookings";

        using var byOwner = await GetAsync(client, court.Owner.AccessToken, path);
        using var byAdmin = await GetAsync(client, otherCourt.Admin.AccessToken, path);
        using var byOtherOwner = await GetAsync(client, otherCourt.Owner.AccessToken, path);
        using var byUser = await GetAsync(client, otherUser.AccessToken, path);
        using var unknownCourt = await GetAsync(client, court.Admin.AccessToken, $"/api/v1/courts/{Guid.NewGuid()}/bookings");

        Assert.Equal([booking.Id], (await ReadListAsync(byOwner)).Select(b => b.Id));
        Assert.Equal([booking.Id], (await ReadListAsync(byAdmin)).Select(b => b.Id));
        Assert.Equal(HttpStatusCode.Forbidden, byOtherOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byUser.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownCourt.StatusCode);
    }

    // ---- helpers ----

    private async Task<(ArrangedCourt Court, AuthApi.AuthDto User, BookingDto Booking)> ArrangeBookingAsync(HttpClient client)
    {
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));
        return (court, user, booking);
    }

    /// <summary>
    /// Pays the booking and moves it into the past so it has ended.
    /// </summary>
    private async Task PayAndEndAsync(HttpClient client, AuthApi.AuthDto user, BookingDto booking)
    {
        using (var pay = await ActionAsync(client, user.AccessToken, booking.Id, "pay"))
        {
            Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        }

        await factory.SetBookingTimesAsync(booking.Id, startsAtUtc: DateTime.UtcNow.AddHours(-2), endsAtUtc: DateTime.UtcNow.AddMinutes(-1));
    }

    private async Task<Booking> StoredAsync(Guid bookingId)
    {
        Booking? booking = null;
        await factory.WithDbAsync(async db => booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId));
        return booking!;
    }
}
