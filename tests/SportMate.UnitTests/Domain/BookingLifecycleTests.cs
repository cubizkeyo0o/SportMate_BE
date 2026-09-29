using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Domain;

public sealed class BookingLifecycleTests
{
    private static readonly TimeSpan Hold = TimeSpan.FromMinutes(15);
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(1);

    private static Booking Create()
    {
        var courtSport = new CourtSport(Guid.NewGuid(), Guid.NewGuid(), 100_000m, "VND", Created);
        return new Booking(Guid.NewGuid(), courtSport, Start, End, 100_000m, Created);
    }

    private static Booking Confirmed()
    {
        var booking = Create();
        booking.Confirm(Hold, Created.AddMinutes(1));
        return booking;
    }

    // ---- hold ----

    [Fact]
    public void NewBooking_IsPendingHoldExpiringAfterHoldDuration()
    {
        var booking = Create();

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(Created.AddMinutes(15), booking.HoldExpiresAtUtc(Hold));
        Assert.True(booking.BlocksAvailabilityAt(Hold, Created.AddMinutes(14)));
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)] // expires exactly at created + hold
    [InlineData(60, true)]
    public void IsHoldExpired_AtOrAfterHoldEnd(int minutesAfterCreation, bool expected)
    {
        var booking = Create();

        Assert.Equal(expected, booking.IsHoldExpired(Hold, Created.AddMinutes(minutesAfterCreation)));
        Assert.Equal(!expected, booking.BlocksAvailabilityAt(Hold, Created.AddMinutes(minutesAfterCreation)));
    }

    [Fact]
    public void ExpireHold_CancelsExpiredPendingBookingWithReason()
    {
        var booking = Create();
        var now = Created.AddMinutes(16);

        Assert.True(booking.ExpireHold(Hold, now));

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(Booking.HoldExpiredReason, booking.CancellationReason);
        Assert.Equal(now, booking.CancelledAtUtc);
        Assert.Equal(now, booking.UpdatedAtUtc);
        Assert.False(booking.BlocksAvailabilityAt(Hold, now));
    }

    [Fact]
    public void ExpireHold_LeavesRunningHoldsAndConfirmedBookingsAlone()
    {
        var pending = Create();
        var confirmed = Confirmed();

        Assert.False(pending.ExpireHold(Hold, Created.AddMinutes(10)));
        Assert.False(confirmed.ExpireHold(Hold, Created.AddDays(1)));

        Assert.Equal(BookingStatus.Pending, pending.Status);
        Assert.Equal(BookingStatus.Confirmed, confirmed.Status);
        Assert.True(confirmed.BlocksAvailabilityAt(Hold, Created.AddDays(1)));
    }

    // ---- confirm (mock payment) ----

    [Fact]
    public void Confirm_PendingWithinHold_BecomesConfirmed()
    {
        var booking = Create();
        var now = Created.AddMinutes(14);

        booking.Confirm(Hold, now);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(now, booking.UpdatedAtUtc);
    }

    [Fact]
    public void Confirm_AfterHoldExpired_IsRejected()
    {
        var booking = Create();

        Assert.Throws<DomainException>(() => booking.Confirm(Hold, Created.AddMinutes(15)));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Theory]
    [InlineData(0)]  // start time reached
    [InlineData(5)]  // start time passed, hold still running
    public void Confirm_AtOrAfterStart_IsRejected(int minutesAfterStart)
    {
        // Created 10 minutes before the start, so the 15-minute hold is still running at the start.
        var courtSport = new CourtSport(Guid.NewGuid(), Guid.NewGuid(), 100_000m, "VND", Created);
        var booking = new Booking(Guid.NewGuid(), courtSport, Start, End, 100_000m, Start.AddMinutes(-10));

        Assert.Throws<DomainException>(() => booking.Confirm(Hold, Start.AddMinutes(minutesAfterStart)));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void CancelUnpaidAfterStart_CancelsWithReason_OnlyOnceStarted()
    {
        var booking = Create();

        Assert.Throws<DomainException>(() => booking.CancelUnpaidAfterStart(Start.AddMinutes(-1)));
        booking.CancelUnpaidAfterStart(Start);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(Booking.StartPassedReason, booking.CancellationReason);
        Assert.Equal(Start, booking.CancelledAtUtc);
        Assert.Throws<DomainException>(() => booking.CancelUnpaidAfterStart(Start.AddMinutes(1)));
    }

    [Fact]
    public void CancelUnpaidAfterStart_RejectsConfirmedBooking()
    {
        var booking = Confirmed();

        Assert.Throws<DomainException>(() => booking.CancelUnpaidAfterStart(Start));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Confirm_NonPending_IsRejected()
    {
        var confirmed = Confirmed();
        var cancelled = Create();
        cancelled.Cancel(null, Created.AddMinutes(1));

        Assert.Throws<DomainException>(() => confirmed.Confirm(Hold, Created.AddMinutes(2)));
        Assert.Throws<DomainException>(() => cancelled.Confirm(Hold, Created.AddMinutes(2)));
    }

    // ---- cancel ----

    [Fact]
    public void Cancel_PendingOrConfirmedBeforeStart_Succeeds()
    {
        var pending = Create();
        var confirmed = Confirmed();
        var now = Start.AddMinutes(-1);

        pending.Cancel("  rain  ", now);
        confirmed.Cancel(null, now);

        Assert.Equal(BookingStatus.Cancelled, pending.Status);
        Assert.Equal("rain", pending.CancellationReason);
        Assert.Equal(now, pending.CancelledAtUtc);
        Assert.Equal(BookingStatus.Cancelled, confirmed.Status);
        Assert.Null(confirmed.CancellationReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void Cancel_AtOrAfterStart_IsRejected(int minutesAfterStart)
    {
        var booking = Confirmed();

        Assert.Throws<DomainException>(() => booking.Cancel(null, Start.AddMinutes(minutesAfterStart)));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Cancel_RejectsReasonLongerThanColumn()
    {
        var booking = Create();

        Assert.Throws<DomainException>(() => booking.Cancel(new string('r', 501), Created.AddMinutes(1)));
    }

    // ---- complete / no-show ----

    [Fact]
    public void Complete_ConfirmedAfterEnd_Succeeds()
    {
        var booking = Confirmed();

        booking.Complete(End);

        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.False(booking.BlocksAvailabilityAt(Hold, End));
    }

    [Fact]
    public void MarkNoShow_ConfirmedAfterEnd_Succeeds()
    {
        var booking = Confirmed();

        booking.MarkNoShow(End.AddHours(1));

        Assert.Equal(BookingStatus.NoShow, booking.Status);
        Assert.False(booking.BlocksAvailabilityAt(Hold, End.AddHours(1)));
    }

    [Fact]
    public void CompleteAndNoShow_BeforeEnd_AreRejected()
    {
        var booking = Confirmed();

        Assert.Throws<DomainException>(() => booking.Complete(End.AddMinutes(-1)));
        Assert.Throws<DomainException>(() => booking.MarkNoShow(End.AddMinutes(-1)));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void CompleteAndNoShow_FromPending_AreRejected()
    {
        var booking = Create();

        Assert.Throws<DomainException>(() => booking.Complete(End));
        Assert.Throws<DomainException>(() => booking.MarkNoShow(End));
    }

    [Fact]
    public void TerminalStatuses_AllowNoFurtherTransition()
    {
        var completed = Confirmed();
        completed.Complete(End);
        var noShow = Confirmed();
        noShow.MarkNoShow(End);
        var cancelled = Create();
        cancelled.Cancel(null, Created.AddMinutes(1));

        foreach (var booking in new[] { completed, noShow, cancelled })
        {
            var status = booking.Status;
            Assert.Throws<DomainException>(() => booking.Confirm(Hold, Created.AddMinutes(2)));
            Assert.Throws<DomainException>(() => booking.Cancel(null, Created.AddMinutes(2)));
            Assert.Throws<DomainException>(() => booking.Complete(End.AddDays(1)));
            Assert.Throws<DomainException>(() => booking.MarkNoShow(End.AddDays(1)));
            Assert.False(booking.ExpireHold(Hold, End.AddDays(1)));
            Assert.Equal(status, booking.Status);
        }
    }

    // ---- price ----

    [Theory]
    [InlineData("100000", 60, "100000.00")]
    [InlineData("150000", 90, "225000.00")]  // fractional hour
    [InlineData("100000", 45, "75000.00")]
    [InlineData("100.01", 50, "83.34")]      // 83.341666...
    [InlineData("0.01", 30, "0.01")]         // 0.005 rounds away from zero (banker's rounding would give 0.00)
    [InlineData("0.03", 30, "0.02")]         // 0.015 -> 0.02
    [InlineData("0", 120, "0.00")]
    [InlineData("99.99", 1, "1.67")]         // 1.6665
    public void CalculateTotalPrice_UsesMinutesAndRoundsAwayFromZero(string pricePerHour, int minutes, string expected)
    {
        var price = decimal.Parse(pricePerHour, System.Globalization.CultureInfo.InvariantCulture);

        var total = Booking.CalculateTotalPrice(price, Start, Start.AddMinutes(minutes));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), total);
    }
}
