using SportMate.Application.Sports;
using SportMate.Domain.Entities;

namespace SportMate.Application.Courts;

/// <summary>
/// A sport offered on a court. <c>Sport.IsActive</c> is the sport's own state; <paramref name="IsActive"/>
/// is whether this court currently offers it. <paramref name="PricePerHour"/> is the current price for one hour.
/// </summary>
public sealed record CourtSportResponse(
    SportSummaryResponse Sport,
    decimal PricePerHour,
    string Currency,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static CourtSportResponse From(CourtSport courtSport, Sport sport) =>
        new(
            new SportSummaryResponse(sport.Id, sport.Name, sport.Slug, sport.IsActive),
            courtSport.PricePerHour,
            courtSport.Currency,
            courtSport.IsActive,
            courtSport.CreatedAtUtc,
            courtSport.UpdatedAtUtc);
}

/// <summary>
/// Adds a sport to a court. The sport must be active; the relationship starts active.
/// </summary>
public sealed record AddCourtSportRequest(Guid? SportId, decimal? PricePerHour, string? Currency);

/// <summary>
/// Full replacement of the editable fields. <paramref name="IsActive"/> cannot become true while the sport
/// itself is inactive.
/// </summary>
public sealed record UpdateCourtSportRequest(decimal? PricePerHour, string? Currency, bool? IsActive);
