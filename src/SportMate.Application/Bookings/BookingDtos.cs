using System.Globalization;
using SportMate.Application.Sports;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Bookings;

/// <summary>
/// A booking. <c>*_utc</c> fields are UTC instants; <c>local_*</c> fields are the same start and end as
/// wall-clock time of the court (<paramref name="TimeZoneId"/>). <paramref name="HoldExpiresAtUtc"/> is set
/// only while the booking is Pending.
/// </summary>
public sealed record BookingResponse(
    Guid Id,
    Guid UserId,
    Guid CourtId,
    Guid CourtSportId,
    SportSummaryResponse Sport,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string TimeZoneId,
    string LocalDate,
    string LocalStartTime,
    string LocalEndTime,
    BookingStatus Status,
    decimal TotalPrice,
    string Currency,
    DateTime? HoldExpiresAtUtc,
    string? CancellationReason,
    DateTime? CancelledAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    /// <summary>
    /// <paramref name="booking"/> must have Court and CourtSport.Sport loaded.
    /// </summary>
    public static BookingResponse From(Booking booking, TimeSpan holdDuration)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(booking.Court.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(booking.StartsAtUtc, zone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(booking.EndsAtUtc, zone);
        var sport = booking.CourtSport.Sport;

        return new BookingResponse(
            booking.Id,
            booking.UserId,
            booking.CourtId,
            booking.CourtSportId,
            new SportSummaryResponse(sport.Id, sport.Name, sport.Slug, sport.IsActive),
            booking.StartsAtUtc,
            booking.EndsAtUtc,
            booking.Court.TimeZoneId,
            localStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            localStart.ToString("HH:mm", CultureInfo.InvariantCulture),
            localEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
            booking.Status,
            booking.TotalPrice,
            booking.Currency,
            booking.Status == BookingStatus.Pending ? booking.HoldExpiresAtUtc(holdDuration) : null,
            booking.CancellationReason,
            booking.CancelledAtUtc,
            booking.CreatedAtUtc,
            booking.UpdatedAtUtc);
    }
}

/// <summary>
/// <paramref name="StartsAt"/> and <paramref name="EndsAt"/> are ISO-8601 timestamps with <c>Z</c> or an explicit
/// offset (e.g. <c>2026-10-01T10:00:00+07:00</c>), in whole minutes. The court, price and status come from
/// the server.
/// </summary>
public sealed record CreateBookingRequest(Guid? CourtSportId, string? StartsAt, string? EndsAt);

public sealed record CancelBookingRequest(string? Reason);
