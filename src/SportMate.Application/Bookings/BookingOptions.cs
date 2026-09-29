namespace SportMate.Application.Bookings;

/// <summary>
/// Bound from the "Booking" configuration section (e.g. environment variable
/// <c>Booking__HoldDurationMinutes</c>). The single source of the hold duration.
/// </summary>
public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public const int DefaultHoldDurationMinutes = 15;

    /// <summary>
    /// How long a new (Pending) booking holds the court while waiting for payment.
    /// </summary>
    public int HoldDurationMinutes { get; set; } = DefaultHoldDurationMinutes;

    public TimeSpan HoldDuration => TimeSpan.FromMinutes(HoldDurationMinutes);
}
