using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Application.Sports;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// Read-only court browsing for every authenticated user (docs/BUSINESS_RULES.md section 2.1). Only Active
/// courts exist here: an inactive or unknown court is reported as not found. Management reads, which also see
/// inactive data and management fields, stay in <see cref="CourtService"/>, <see cref="CourtSportService"/>
/// and <see cref="CourtOperatingHourService"/>.
/// </summary>
public sealed class CourtDiscoveryService(IApplicationDbContext db)
{
    /// <summary>
    /// All Active courts, ordered by name. A court is listed even if it has no sports or hours yet.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredCourtResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Courts
            .AsNoTracking()
            .Where(c => c.Status == CourtStatus.Active)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new DiscoveredCourtResponse(
                c.Id, c.Name, c.Description, c.Address, c.City, c.Latitude, c.Longitude, c.TimeZoneId, c.Status))
            .ToListAsync(cancellationToken);

    public async Task<DiscoveredCourtResponse> GetAsync(Guid courtId, CancellationToken cancellationToken) =>
        await db.Courts
            .AsNoTracking()
            .Where(c => c.Id == courtId && c.Status == CourtStatus.Active)
            .Select(c => new DiscoveredCourtResponse(
                c.Id, c.Name, c.Description, c.Address, c.City, c.Latitude, c.Longitude, c.TimeZoneId, c.Status))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw CourtNotFound();

    /// <summary>
    /// The sports the court offers now: active court-sport links whose sport is also active, by sport name.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredCourtSportResponse>> ListSportsAsync(
        Guid courtId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveCourtAsync(courtId, cancellationToken);

        return await db.CourtSports
            .AsNoTracking()
            .Where(cs => cs.CourtId == courtId && cs.IsActive && cs.Sport.IsActive)
            .OrderBy(cs => cs.Sport.Name)
            .ThenBy(cs => cs.SportId)
            .Select(cs => new DiscoveredCourtSportResponse(
                new SportResponse(cs.Sport.Id, cs.Sport.Name, cs.Sport.Slug),
                cs.PricePerHour,
                cs.Currency,
                cs.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The court's weekly periods in its local time, as configured, ordered by day then opening time.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredOperatingHourResponse>> ListOperatingHoursAsync(
        Guid courtId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveCourtAsync(courtId, cancellationToken);

        var hours = await db.CourtOperatingHours
            .AsNoTracking()
            .Where(h => h.CourtId == courtId)
            .Select(h => new { h.DayOfWeek, h.OpensAt, h.ClosesAt })
            .ToListAsync(cancellationToken);

        return hours
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.OpensAt)
            .Select(h => new DiscoveredOperatingHourResponse(
                (int)h.DayOfWeek,
                CourtOperatingHourInputRules.Format(h.OpensAt),
                CourtOperatingHourInputRules.Format(h.ClosesAt)))
            .ToList();
    }

    private async Task EnsureActiveCourtAsync(Guid courtId, CancellationToken cancellationToken)
    {
        if (!await db.Courts.AnyAsync(c => c.Id == courtId && c.Status == CourtStatus.Active, cancellationToken))
        {
            throw CourtNotFound();
        }
    }

    // Same message for inactive and unknown courts, so discovery does not reveal that an inactive court exists.
    private static NotFoundException CourtNotFound() => new("Court not found.");
}
