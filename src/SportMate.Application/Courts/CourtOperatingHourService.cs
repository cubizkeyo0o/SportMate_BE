using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// A court's recurring weekly opening periods. Managed by Admin or the court's assigned CourtOwner
/// (<see cref="CourtAccess"/>). Periods of the same day must not overlap; touching periods are fine.
/// </summary>
public sealed class CourtOperatingHourService(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<OperatingHourResponse>> ListAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var hours = await db.CourtOperatingHours
            .AsNoTracking()
            .Where(h => h.CourtId == courtId)
            .ToListAsync(cancellationToken);

        return hours
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.OpensAt)
            .Select(OperatingHourResponse.From)
            .ToList();
    }

    public async Task<OperatingHourResponse> AddAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        OperatingHourRequest request,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);

        var errors = new ValidationErrors();
        var (day, opensAt, closesAt) = CourtOperatingHourInputRules.Period(request, errors);
        errors.ThrowIfAny();
        await EnsureNoOverlapAsync(courtId, null, day, opensAt, closesAt, cancellationToken);

        var hour = new CourtOperatingHour(courtId, day, opensAt, closesAt, timeProvider.GetUtcNow().UtcDateTime);
        db.CourtOperatingHours.Add(hour);
        await SaveAsync(cancellationToken);
        return OperatingHourResponse.From(hour);
    }

    public async Task<OperatingHourResponse> UpdateAsync(
        Guid courtId,
        Guid hourId,
        Guid actorId,
        UserRole actorRole,
        OperatingHourRequest request,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);
        var hour = await FindAsync(courtId, hourId, cancellationToken);

        var errors = new ValidationErrors();
        var (day, opensAt, closesAt) = CourtOperatingHourInputRules.Period(request, errors);
        errors.ThrowIfAny();
        await EnsureNoOverlapAsync(courtId, hourId, day, opensAt, closesAt, cancellationToken);

        hour.Reschedule(day, opensAt, closesAt, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync(cancellationToken);
        return OperatingHourResponse.From(hour);
    }

    /// <summary>
    /// Operating hours are configuration, not history, so they are deleted outright.
    /// </summary>
    public async Task DeleteAsync(
        Guid courtId,
        Guid hourId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        await CourtAccess.EnsureCanManageAsync(db, courtId, actorId, actorRole, cancellationToken);
        var hour = await FindAsync(courtId, hourId, cancellationToken);
        db.CourtOperatingHours.Remove(hour);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CourtOperatingHour> FindAsync(Guid courtId, Guid hourId, CancellationToken cancellationToken) =>
        await db.CourtOperatingHours.FirstOrDefaultAsync(h => h.Id == hourId && h.CourtId == courtId, cancellationToken)
            ?? throw new NotFoundException("Operating hours not found.");

    private async Task EnsureNoOverlapAsync(
        Guid courtId,
        Guid? excludedHourId,
        DayOfWeek day,
        TimeOnly opensAt,
        TimeOnly closesAt,
        CancellationToken cancellationToken)
    {
        var sameDay = await db.CourtOperatingHours
            .AsNoTracking()
            .Where(h => h.CourtId == courtId && h.DayOfWeek == day && h.Id != excludedHourId)
            .ToListAsync(cancellationToken);

        var clash = sameDay.OrderBy(h => h.OpensAt).FirstOrDefault(h => h.Overlaps(day, opensAt, closesAt));
        if (clash is not null)
        {
            var errors = new ValidationErrors();
            errors.Add(
                "opens_at",
                $"This period overlaps the existing period {CourtOperatingHourInputRules.Format(clash.OpensAt)}-"
                + $"{CourtOperatingHourInputRules.Format(clash.ClosesAt)} on the same day.");
            errors.ThrowIfAny();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request saved a period with the same opening time; the unique index rejected ours.
            throw new ConflictException("The court's operating hours were changed by another request. Please retry.");
        }
    }
}
