using System.Data;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Application.Courts;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Bookings;

/// <summary>
/// Booking use cases. <c>actorId</c>/<c>actorRole</c> identify the authenticated caller (from the access token,
/// checked against the database on every request); the booker is always the caller.
/// </summary>
/// <remarks>
/// Invariant: every write to a booking (creation and every status change, including hold expiry) happens in a
/// READ COMMITTED transaction that holds the booking's physical court row lock
/// (<see cref="IApplicationDbContext.LockCourtForUpdateAsync"/>), and the booking is re-read after the lock is
/// taken. Changes to the bookings of one court are therefore strictly serialized, so exactly one transition out
/// of Pending (to Confirmed or to Cancelled) can win. Expired holds are cancelled lazily whenever bookings are
/// read or changed; there is no background job.
/// </remarks>
public sealed class BookingService(IApplicationDbContext db, TimeProvider timeProvider, BookingOptions options)
{
    private const string StartPassedMessage =
        "The booking start time has passed, so it was cancelled and can no longer be paid.";

    private TimeSpan Hold => options.HoldDuration;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<BookingResponse> CreateAsync(
        Guid userId,
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (request.CourtSportId is null)
        {
            errors.Add("court_sport_id", "Court sport is required.");
        }

        var startsAt = BookingInputRules.Timestamp(request.StartsAt, "starts_at", errors);
        var endsAt = BookingInputRules.Timestamp(request.EndsAt, "ends_at", errors);
        if (startsAt is not null && endsAt is not null)
        {
            ValidateTimes(startsAt.Value, endsAt.Value, UtcNow, errors);
        }

        errors.ThrowIfAny();
        var start = startsAt!.Value;
        var end = endsAt!.Value;
        var courtSportId = request.CourtSportId!.Value;

        // The physical court is resolved from the court sport, never taken from the client.
        var courtId = await db.CourtSports
            .Where(cs => cs.Id == courtSportId)
            .Select(cs => (Guid?)cs.CourtId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Court sport not found.");

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.LockCourtForUpdateAsync(courtId, cancellationToken);

        // Everything below is read after the lock, so it cannot change until this transaction ends.
        var now = UtcNow;
        var courtSport = await db.CourtSports
            .Include(cs => cs.Court)
            .Include(cs => cs.Sport)
            .FirstAsync(cs => cs.Id == courtSportId, cancellationToken);
        var hours = await db.CourtOperatingHours
            .AsNoTracking()
            .Where(h => h.CourtId == courtId)
            .ToListAsync(cancellationToken);

        ValidateTimes(start, end, now, errors);
        ValidateBookable(courtSport, errors);
        ValidateSchedule(courtSport.Court, hours, start, end, errors);
        var totalPrice = Booking.CalculateTotalPrice(courtSport.PricePerHour, start, end);
        if (totalPrice > CourtSportInputRules.MaxPrice)
        {
            errors.Add("ends_at", "The booking is too long: its total price exceeds the maximum amount.");
        }

        errors.ThrowIfAny();

        await ExpireHoldsUnderLockAsync([courtId], b => b.CourtId == courtId, now, cancellationToken);
        var cutoff = now - Hold;
        var conflict = await db.Bookings.AnyAsync(
            b => b.CourtId == courtId
                 && b.StartsAtUtc < end
                 && b.EndsAtUtc > start
                 && (b.Status == BookingStatus.Confirmed
                     || (b.Status == BookingStatus.Pending && b.CreatedAtUtc > cutoff)),
            cancellationToken);
        if (conflict)
        {
            throw new ConflictException("The court is already booked for part of this time.");
        }

        var booking = new Booking(userId, courtSport, start, end, totalPrice, now);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BookingResponse.From(booking, Hold);
    }

    public async Task<BookingResponse> GetAsync(
        Guid bookingId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await GetAccessAsync(bookingId, actorId, actorRole, cancellationToken);
        await ExpireHoldsAsync(b => b.Id == bookingId, cancellationToken);
        var booking = await WithDetails(db.Bookings.AsNoTracking()).FirstAsync(b => b.Id == bookingId, cancellationToken);
        return BookingResponse.From(booking, Hold);
    }

    /// <summary>
    /// The caller's own bookings, ordered by start time.
    /// </summary>
    public async Task<IReadOnlyList<BookingResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        await ExpireHoldsAsync(b => b.UserId == userId, cancellationToken);
        return await ListAsync(b => b.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// All bookings of a court, for Admin or the court's assigned CourtOwner.
    /// </summary>
    public async Task<IReadOnlyList<BookingResponse>> ListForCourtAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);
        await ExpireHoldsAsync(b => b.CourtId == courtId, cancellationToken);
        return await ListAsync(b => b.CourtId == courtId, cancellationToken);
    }

    /// <summary>
    /// Mock payment: Pending → Confirmed while the hold is running and before the booking starts. Only the booker
    /// may pay. An unpaid booking whose start time has been reached is cancelled instead.
    /// </summary>
    public Task<BookingResponse> PayAsync(Guid bookingId, Guid actorId, UserRole actorRole, CancellationToken cancellationToken) =>
        ChangeAsync(bookingId, actorId, actorRole, (booking, access, now) =>
        {
            if (!access.IsBooker)
            {
                throw new ForbiddenException("Only the person who made the booking can pay for it.");
            }

            switch (booking.Status)
            {
                case BookingStatus.Pending when now >= booking.StartsAtUtc:
                    // Saved even though the request fails (see ChangeAsync).
                    booking.CancelUnpaidAfterStart(now);
                    throw StatusError(StartPassedMessage);
                case BookingStatus.Pending:
                    booking.Confirm(Hold, now);
                    break;
                case BookingStatus.Cancelled when booking.CancellationReason == Booking.HoldExpiredReason:
                    throw StatusError("The hold on this booking has expired, so it was cancelled and can no longer be paid.");
                case BookingStatus.Cancelled when booking.CancellationReason == Booking.StartPassedReason:
                    throw StatusError(StartPassedMessage);
                case BookingStatus.Confirmed:
                    throw StatusError("This booking has already been paid and confirmed.");
                default:
                    throw StatusError($"A {Describe(booking.Status)} booking cannot be paid.");
            }
        }, cancellationToken);

    /// <summary>
    /// Pending or Confirmed → Cancelled before the booking starts. The booker, the court's assigned
    /// CourtOwner and Admin may cancel.
    /// </summary>
    public Task<BookingResponse> CancelAsync(
        Guid bookingId,
        Guid actorId,
        UserRole actorRole,
        CancelBookingRequest? request,
        CancellationToken cancellationToken) =>
        ChangeAsync(bookingId, actorId, actorRole, (booking, _, now) =>
        {
            var errors = new ValidationErrors();
            var reason = BookingInputRules.CancellationReason(request?.Reason, errors);
            errors.ThrowIfAny();

            if (!booking.BlocksAvailability)
            {
                throw StatusError(booking.CancellationReason == Booking.HoldExpiredReason
                    ? "The hold on this booking has already expired and the booking was cancelled."
                    : $"A {Describe(booking.Status)} booking cannot be cancelled.");
            }

            if (now >= booking.StartsAtUtc)
            {
                throw StatusError("A booking cannot be cancelled once it has started.");
            }

            booking.Cancel(reason, now);
        }, cancellationToken);

    /// <summary>
    /// Confirmed → Completed after the booking ends. Court's assigned CourtOwner or Admin only.
    /// </summary>
    public Task<BookingResponse> CompleteAsync(Guid bookingId, Guid actorId, UserRole actorRole, CancellationToken cancellationToken) =>
        ChangeAsync(bookingId, actorId, actorRole, (booking, access, now) =>
        {
            EnsureEndedConfirmed(booking, access, now, "completed");
            booking.Complete(now);
        }, cancellationToken);

    /// <summary>
    /// Confirmed → NoShow after the booking ends. Court's assigned CourtOwner or Admin only.
    /// </summary>
    public Task<BookingResponse> MarkNoShowAsync(Guid bookingId, Guid actorId, UserRole actorRole, CancellationToken cancellationToken) =>
        ChangeAsync(bookingId, actorId, actorRole, (booking, access, now) =>
        {
            EnsureEndedConfirmed(booking, access, now, "marked as no-show");
            booking.MarkNoShow(now);
        }, cancellationToken);

    private static void ValidateTimes(DateTime start, DateTime end, DateTime now, ValidationErrors errors)
    {
        if (start >= end)
        {
            errors.Add("ends_at", "End time must be later than start time.");
        }
        else if (start <= now)
        {
            errors.Add("starts_at", "Start time must be in the future.");
        }
    }

    private static void ValidateBookable(CourtSport courtSport, ValidationErrors errors)
    {
        if (courtSport.Court.Status != CourtStatus.Active)
        {
            errors.Add("court_sport_id", "This court is not accepting bookings.");
        }
        else if (!courtSport.IsActive)
        {
            errors.Add("court_sport_id", "This court no longer offers this sport.");
        }
        else if (!courtSport.Sport.IsActive)
        {
            errors.Add("court_sport_id", "This sport is not available for booking.");
        }
    }

    /// <summary>
    /// The booking, in the court's local time, must be on one local day, avoid daylight-saving changes and
    /// ambiguous times, and lie inside the union of that day's operating periods.
    /// </summary>
    private static void ValidateSchedule(
        Court court,
        IReadOnlyList<CourtOperatingHour> hours,
        DateTime start,
        DateTime end,
        ValidationErrors errors)
    {
        if (errors.HasErrors)
        {
            return;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(court.TimeZoneId, out var zone))
        {
            errors.Add("court_sport_id", "The court's time zone is not configured correctly.");
            return;
        }

        var localStart = TimeZoneInfo.ConvertTimeFromUtc(start, zone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(end, zone);

        if (zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd))
        {
            errors.Add("starts_at", "The booking falls on a local time that occurs twice (daylight-saving change).");
        }
        else if (zone.GetUtcOffset(start) != zone.GetUtcOffset(end))
        {
            errors.Add("starts_at", "The booking spans a daylight-saving change, which skips or repeats local time.");
        }
        else if (localStart.Date != localEnd.Date)
        {
            errors.Add("ends_at", "A booking cannot cross midnight in the court's local time.");
        }
        else
        {
            var periods = hours.Where(h => h.DayOfWeek == localStart.DayOfWeek).ToList();
            if (periods.Count == 0)
            {
                errors.Add("starts_at", $"The court has no operating hours on {localStart.DayOfWeek}.");
            }
            else if (!CourtOperatingHour.Covers(periods, TimeOnly.FromDateTime(localStart), TimeOnly.FromDateTime(localEnd)))
            {
                errors.Add("starts_at", "The booking is outside the court's operating hours.");
            }
        }
    }

    private static void EnsureEndedConfirmed(Booking booking, Access access, DateTime now, string action)
    {
        if (!access.IsCourtManager)
        {
            throw new ForbiddenException($"Only the court's owner or an Admin can mark a booking as {action}.");
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            throw StatusError($"Only confirmed bookings can be {action}; this booking is {Describe(booking.Status)}.");
        }

        if (now < booking.EndsAtUtc)
        {
            throw StatusError($"A booking can only be {action} after it has ended.");
        }
    }

    /// <summary>
    /// Runs a status change on one booking under the court lock. An expired hold is cancelled first; that, and
    /// any cancellation the change makes before rejecting the request, is saved even if the change is rejected.
    /// </summary>
    private async Task<BookingResponse> ChangeAsync(
        Guid bookingId,
        Guid actorId,
        UserRole actorRole,
        Action<Booking, Access, DateTime> change,
        CancellationToken cancellationToken)
    {
        var (courtId, access) = await GetAccessAsync(bookingId, actorId, actorRole, cancellationToken);

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.LockCourtForUpdateAsync(courtId, cancellationToken);

        var now = UtcNow;
        var booking = await WithDetails(db.Bookings).FirstAsync(b => b.Id == bookingId, cancellationToken);
        booking.ExpireHold(Hold, now);

        ExceptionDispatchInfo? rejection = null;
        try
        {
            change(booking, access, now);
        }
        catch (Exception exception) when (exception is ValidationException or ForbiddenException)
        {
            rejection = ExceptionDispatchInfo.Capture(exception);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        rejection?.Throw();
        return BookingResponse.From(booking, Hold);
    }

    /// <summary>
    /// The booker, Admin and the court's assigned CourtOwner can see a booking. Anyone else gets 404,
    /// so booking ids are not revealed.
    /// </summary>
    private async Task<(Guid CourtId, Access Access)> GetAccessAsync(
        Guid bookingId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        var row = await db.Bookings
            .Where(b => b.Id == bookingId)
            .Select(b => new { b.UserId, b.CourtId, CourtOwnerId = b.Court.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        var access = row is null
            ? default
            : new Access(
                IsBooker: row.UserId == actorId,
                IsCourtManager: actorRole == UserRole.Admin
                                || (actorRole == UserRole.CourtOwner && row.CourtOwnerId == actorId));

        if (row is null || !(access.IsBooker || access.IsCourtManager))
        {
            throw new NotFoundException("Booking not found.");
        }

        return (row.CourtId, access);
    }

    /// <summary>
    /// Cancels the expired holds in scope before a read. Nothing is written unless a read-only check finds
    /// expired holds; then the courts they belong to are locked (in a fixed order, so concurrent sweeps cannot
    /// deadlock) and the holds are cancelled under those locks, like every other booking write.
    /// </summary>
    private async Task ExpireHoldsAsync(Expression<Func<Booking, bool>> scope, CancellationToken cancellationToken)
    {
        var cutoff = UtcNow - Hold;
        var courtIds = await db.Bookings
            .Where(scope)
            .Where(b => b.Status == BookingStatus.Pending && b.CreatedAtUtc <= cutoff)
            .Select(b => b.CourtId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        if (courtIds.Count == 0)
        {
            return;
        }

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        foreach (var courtId in courtIds)
        {
            await db.LockCourtForUpdateAsync(courtId, cancellationToken);
        }

        await ExpireHoldsUnderLockAsync(courtIds, scope, UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Cancels every expired Pending booking in scope on the given courts, whose rows the caller has locked.
    /// A single conditional UPDATE; mirrors <see cref="Booking.ExpireHold"/>.
    /// </summary>
    private Task<int> ExpireHoldsUnderLockAsync(
        IReadOnlyCollection<Guid> lockedCourtIds,
        Expression<Func<Booking, bool>> scope,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now - Hold;
        return db.Bookings
            .Where(scope)
            .Where(b => lockedCourtIds.Contains(b.CourtId)
                        && b.Status == BookingStatus.Pending
                        && b.CreatedAtUtc <= cutoff)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(b => b.Status, BookingStatus.Cancelled)
                    .SetProperty(b => b.CancellationReason, Booking.HoldExpiredReason)
                    .SetProperty(b => b.CancelledAtUtc, now)
                    .SetProperty(b => b.UpdatedAtUtc, now),
                cancellationToken);
    }

    private async Task<IReadOnlyList<BookingResponse>> ListAsync(
        Expression<Func<Booking, bool>> filter,
        CancellationToken cancellationToken)
    {
        var bookings = await WithDetails(db.Bookings.AsNoTracking())
            .Where(filter)
            .OrderBy(b => b.StartsAtUtc)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
        return bookings.Select(b => BookingResponse.From(b, Hold)).ToList();
    }

    private static IQueryable<Booking> WithDetails(IQueryable<Booking> bookings) =>
        bookings.Include(b => b.Court).Include(b => b.CourtSport).ThenInclude(cs => cs.Sport);

    private static ValidationException StatusError(string message) =>
        new(new Dictionary<string, string[]> { ["status"] = [message] });

    private static string Describe(BookingStatus status) => status switch
    {
        BookingStatus.NoShow => "no-show",
        _ => status.ToString().ToLowerInvariant()
    };

    private readonly record struct Access(bool IsBooker, bool IsCourtManager);
}
