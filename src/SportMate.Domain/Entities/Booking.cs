using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

/// <summary>
/// A reservation of a physical court for one of its sports. Never hard-deleted;
/// history is preserved through <see cref="Status"/>.
/// </summary>
/// <remarks>
/// Overlap checks alone are not safe under concurrency. Callers creating or modifying
/// a booking must lock the <c>courts</c> row (SELECT ... FOR UPDATE) inside a transaction
/// before checking for overlaps.
/// </remarks>
public sealed class Booking : Entity
{
    public const int CancellationReasonMaxLength = 500;

    /// <summary>
    /// Statuses that block availability of the physical court.
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

    public decimal TotalPrice { get; private set; }

    public string Currency { get; private set; } = null!;

    public BookingStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool BlocksAvailability => BlockingStatuses.Contains(Status);

    public bool Overlaps(DateTime requestedStartUtc, DateTime requestedEndUtc) =>
        StartsAtUtc < requestedEndUtc && EndsAtUtc > requestedStartUtc;

    public void Confirm(DateTime utcNow)
    {
        EnsureStatus(BookingStatus.Pending, "Only pending bookings can be confirmed.");
        Status = BookingStatus.Confirmed;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void Cancel(string? reason, DateTime utcNow)
    {
        if (!BlocksAvailability)
        {
            throw new DomainException("Only pending or confirmed bookings can be cancelled.");
        }

        Status = BookingStatus.Cancelled;
        CancellationReason = Guard.Optional(reason, CancellationReasonMaxLength, nameof(reason));
        CancelledAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public void Complete(DateTime utcNow)
    {
        EnsureStatus(BookingStatus.Confirmed, "Only confirmed bookings can be completed.");
        Status = BookingStatus.Completed;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void MarkNoShow(DateTime utcNow)
    {
        EnsureStatus(BookingStatus.Confirmed, "Only confirmed bookings can be marked as no-show.");
        Status = BookingStatus.NoShow;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    private void EnsureStatus(BookingStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(message);
        }
    }
}
