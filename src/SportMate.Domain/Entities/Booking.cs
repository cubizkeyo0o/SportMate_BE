using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

/// <summary>
/// A reservation of a physical court for one of its sports. Never hard-deleted;
/// history is preserved through <see cref="Status"/>.
/// </summary>
/// <remarks>
/// A new booking is <see cref="BookingStatus.Pending"/>: a hold on the court that lasts a configured
/// duration from <see cref="CreatedAtUtc"/> and becomes <see cref="BookingStatus.Confirmed"/> when paid.
/// An unpaid hold expires and is cancelled. Overlap checks alone are not safe under concurrency: callers
/// creating a booking must lock the <c>courts</c> row (SELECT ... FOR UPDATE) inside a transaction first.
/// </remarks>
public sealed class Booking : Entity
{
    public const int CancellationReasonMaxLength = 500;

    /// <summary>
    /// Cancellation reason recorded when an unpaid hold runs out.
    /// </summary>
    public const string HoldExpiredReason = "Hold expired";

    /// <summary>
    /// Statuses that can block availability of the physical court. A Pending booking blocks only while
    /// its hold is running (see <see cref="BlocksAvailabilityAt"/>).
    /// </summary>
    public static readonly IReadOnlyList<BookingStatus> BlockingStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed];

    private Booking()
    {
    }

    public Booking(
        Guid userId,
        CourtSport courtSport,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        decimal totalPrice,
        DateTime utcNow)
    {
        Guard.UtcRange(startsAtUtc, endsAtUtc);
        UserId = userId;
        CourtId = courtSport.CourtId;
        CourtSportId = courtSport.Id;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        TotalPrice = Guard.Money(totalPrice, nameof(totalPrice), allowZero: true);
        Currency = courtSport.Currency;
        Status = BookingStatus.Pending;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>
    /// The physical court. Always equals the court of <see cref="CourtSport"/>
    /// (enforced by a composite foreign key).
    /// </summary>
    public Guid CourtId { get; private set; }

    public Court Court { get; private set; } = null!;

    public Guid CourtSportId { get; private set; }

    public CourtSport CourtSport { get; private set; } = null!;

    public DateTime StartsAtUtc { get; private set; }

    public DateTime EndsAtUtc { get; private set; }

    /// <summary>
    /// Price snapshot taken at creation; later court-sport price changes do not affect it.
    /// </summary>
    public decimal TotalPrice { get; private set; }

    public string Currency { get; private set; } = null!;

    public BookingStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Status-only view: Pending or Confirmed. Use <see cref="BlocksAvailabilityAt"/> for conflict decisions.
    /// </summary>
    public bool BlocksAvailability => BlockingStatuses.Contains(Status);

    /// <summary>
    /// <c>price_per_hour * minutes / 60</c>, rounded to 2 decimals, midpoint away from zero.
    /// </summary>
    public static decimal CalculateTotalPrice(decimal pricePerHour, DateTime startsAtUtc, DateTime endsAtUtc)
    {
        var minutes = (decimal)(endsAtUtc - startsAtUtc).Ticks / TimeSpan.TicksPerMinute;
        return decimal.Round(pricePerHour * minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// When the hold of a Pending booking ends: <see cref="CreatedAtUtc"/> plus the hold duration.
    /// </summary>
    public DateTime HoldExpiresAtUtc(TimeSpan holdDuration) => CreatedAtUtc + holdDuration;

    public bool IsHoldExpired(TimeSpan holdDuration, DateTime utcNow) =>
        Status == BookingStatus.Pending && HoldExpiresAtUtc(holdDuration) <= utcNow;

    /// <summary>
    /// Confirmed bookings, and Pending bookings whose hold is still running, occupy the court.
    /// </summary>
    public bool BlocksAvailabilityAt(TimeSpan holdDuration, DateTime utcNow) =>
        Status == BookingStatus.Confirmed
        || (Status == BookingStatus.Pending && !IsHoldExpired(holdDuration, utcNow));

    public bool Overlaps(DateTime requestedStartUtc, DateTime requestedEndUtc) =>
        StartsAtUtc < requestedEndUtc && EndsAtUtc > requestedStartUtc;

    /// <summary>
    /// Cancels a Pending booking whose hold has run out. Returns false (and changes nothing) otherwise.
    /// </summary>
    public bool ExpireHold(TimeSpan holdDuration, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (!IsHoldExpired(holdDuration, utcNow))
        {
            return false;
        }

        Status = BookingStatus.Cancelled;
        CancellationReason = HoldExpiredReason;
        CancelledAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        return true;
    }

    /// <summary>
    /// Mock payment: Pending → Confirmed while the hold is running.
    /// </summary>
    public void Confirm(TimeSpan holdDuration, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        EnsureStatus(BookingStatus.Pending, "Only pending bookings can be confirmed.");
        if (IsHoldExpired(holdDuration, utcNow))
        {
            throw new DomainException("The hold of this booking has expired.");
        }

        Status = BookingStatus.Confirmed;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Pending or Confirmed → Cancelled, only before the booking starts.
    /// </summary>
    public void Cancel(string? reason, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (!BlocksAvailability)
        {
            throw new DomainException("Only pending or confirmed bookings can be cancelled.");
        }

        if (utcNow >= StartsAtUtc)
        {
            throw new DomainException("A booking cannot be cancelled once it has started.");
        }

        Status = BookingStatus.Cancelled;
        CancellationReason = Guard.Optional(reason, CancellationReasonMaxLength, nameof(reason));
        CancelledAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Confirmed → Completed, only once the booking has ended.
    /// </summary>
    public void Complete(DateTime utcNow)
    {
        EnsureEndedConfirmed(utcNow, "completed");
        Status = BookingStatus.Completed;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Confirmed → NoShow, only once the booking has ended.
    /// </summary>
    public void MarkNoShow(DateTime utcNow)
    {
        EnsureEndedConfirmed(utcNow, "marked as no-show");
        Status = BookingStatus.NoShow;
        UpdatedAtUtc = utcNow;
    }

    private void EnsureEndedConfirmed(DateTime utcNow, string action)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        EnsureStatus(BookingStatus.Confirmed, $"Only confirmed bookings can be {action}.");
        if (utcNow < EndsAtUtc)
        {
            throw new DomainException($"A booking can only be {action} after it has ended.");
        }
    }

    private void EnsureStatus(BookingStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(message);
        }
    }
}
