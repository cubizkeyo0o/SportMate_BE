using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// The sports a court offers. Managed by Admin or the court's assigned CourtOwner (<see cref="CourtAccess"/>).
/// Relationships are identified by (court, sport) and are never hard-deleted.
/// </summary>
public sealed class CourtSportService(IApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Every relationship of the court, active or not, ordered by sport name.
    /// </summary>
    public async Task<IReadOnlyList<CourtSportResponse>> ListAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var rows = await db.CourtSports
            .AsNoTracking()
            .Where(cs => cs.CourtId == courtId)
            .OrderBy(cs => cs.Sport.Name)
            .ThenBy(cs => cs.SportId)
            .Select(cs => new { CourtSport = cs, cs.Sport })
            .ToListAsync(cancellationToken);

        return rows.Select(r => CourtSportResponse.From(r.CourtSport, r.Sport)).ToList();
    }

    public async Task<CourtSportResponse> AddAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        AddCourtSportRequest request,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var errors = new ValidationErrors();
        if (request.SportId is null)
        {
            errors.Add("sport_id", "Sport is required.");
        }

        var price = CourtSportInputRules.PricePerHour(request.PricePerHour, errors);
        var currency = CourtSportInputRules.Currency(request.Currency, errors);
        errors.ThrowIfAny();

        var sportId = request.SportId!.Value;
        var sport = await db.Sports.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sportId, cancellationToken)
            ?? throw new NotFoundException("Sport not found.");

        if (!sport.IsActive)
        {
            errors.Add("sport_id", "This sport is no longer available for new court configurations.");
            errors.ThrowIfAny();
        }

        if (await db.CourtSports.AnyAsync(cs => cs.CourtId == courtId && cs.SportId == sportId, cancellationToken))
        {
            throw new ConflictException(
                "This court already has this sport. Update it instead (set is_active to true to reactivate it).");
        }

        var courtSport = new CourtSport(courtId, sportId, price, currency, timeProvider.GetUtcNow().UtcDateTime);
        db.CourtSports.Add(courtSport);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request added the same (court, sport) first; the unique index rejected ours.
            throw new ConflictException("This court already has this sport.");
        }

        return CourtSportResponse.From(courtSport, sport);
    }

    /// <summary>
    /// Replaces price, currency and active state. Reactivating is rejected while the sport is inactive;
    /// deactivating and price changes are always allowed.
    /// </summary>
    public async Task<CourtSportResponse> UpdateAsync(
        Guid courtId,
        Guid sportId,
        Guid actorId,
        UserRole actorRole,
        UpdateCourtSportRequest request,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var courtSport = await db.CourtSports
            .Include(cs => cs.Sport)
            .FirstOrDefaultAsync(cs => cs.CourtId == courtId && cs.SportId == sportId, cancellationToken)
            ?? throw new NotFoundException("This court does not have this sport.");

        var errors = new ValidationErrors();
        var price = CourtSportInputRules.PricePerHour(request.PricePerHour, errors);
        var currency = CourtSportInputRules.Currency(request.Currency, errors);
        var isActive = CourtSportInputRules.IsActive(request.IsActive, errors);
        if (isActive && !courtSport.IsActive && !courtSport.Sport.IsActive)
        {
            errors.Add("is_active", "The sport is inactive, so it cannot be reactivated on this court.");
        }

        errors.ThrowIfAny();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        courtSport.ChangePrice(price, currency, utcNow);
        courtSport.SetActive(isActive, utcNow);
        await db.SaveChangesAsync(cancellationToken);
        return CourtSportResponse.From(courtSport, courtSport.Sport);
    }

    /// <summary>
    /// Removing a sport from a court only deactivates the relationship; the row and its history stay.
    /// Idempotent: an inactive or missing relationship also succeeds.
    /// </summary>
    public async Task DeactivateAsync(
        Guid courtId,
        Guid sportId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var courtSport = await db.CourtSports
            .FirstOrDefaultAsync(cs => cs.CourtId == courtId && cs.SportId == sportId, cancellationToken);
        if (courtSport is null)
        {
            return;
        }

        courtSport.SetActive(false, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
    }
}
