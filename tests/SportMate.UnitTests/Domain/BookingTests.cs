using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Domain;

public sealed class BookingTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TenAm = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Booking CreateBooking(DateTime start, DateTime end)
    {
        var courtSport = new CourtSport(Guid.NewGuid(), Guid.NewGuid(), 100_000m, "vnd", Now);
        return new Booking(Guid.NewGuid(), courtSport, start, end, 100_000m, Now);
    }

    [Theory]
    [InlineData(30, 90, true)]   // 10:30-11:30 overlaps 10:00-11:00
    [InlineData(-30, 30, true)]  // 09:30-10:30
    [InlineData(15, 45, true)]   // fully inside
    [InlineData(60, 120, false)] // starts exactly when existing ends
    [InlineData(-60, 0, false)]  // ends exactly when existing starts
    public void Overlaps_UsesHalfOpenIntervals(int startOffsetMinutes, int endOffsetMinutes, bool expected)
    {
        var existing = CreateBooking(TenAm, TenAm.AddHours(1));

        var overlaps = existing.Overlaps(TenAm.AddMinutes(startOffsetMinutes), TenAm.AddMinutes(endOffsetMinutes));

        Assert.Equal(expected, overlaps);
    }

    [Fact]
    public void Constructor_TakesCourtAndCurrencyFromCourtSport()
    {
        var courtSport = new CourtSport(Guid.NewGuid(), Guid.NewGuid(), 50m, "usd", Now);

        var booking = new Booking(Guid.NewGuid(), courtSport, TenAm, TenAm.AddHours(1), 50m, Now);

        Assert.Equal(courtSport.CourtId, booking.CourtId);
        Assert.Equal(courtSport.Id, booking.CourtSportId);
        Assert.Equal("USD", booking.Currency);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Constructor_RejectsStartNotBeforeEnd()
    {
        Assert.Throws<DomainException>(() => CreateBooking(TenAm, TenAm));
    }

    [Fact]
    public void Constructor_RejectsNonUtcTimes()
    {
        var local = DateTime.SpecifyKind(TenAm, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => CreateBooking(local, local.AddHours(1)));
    }

    [Fact]
    public void Cancel_StopsBlockingAvailability()
    {
        var booking = CreateBooking(TenAm, TenAm.AddHours(1));
        Assert.True(booking.BlocksAvailability);

        booking.Cancel("changed plans", Now);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.False(booking.BlocksAvailability);
        Assert.Equal(Now, booking.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_RejectsAlreadyCancelledBooking()
    {
        var booking = CreateBooking(TenAm, TenAm.AddHours(1));
        booking.Cancel(null, Now);

        Assert.Throws<DomainException>(() => booking.Cancel(null, Now));
    }
}
