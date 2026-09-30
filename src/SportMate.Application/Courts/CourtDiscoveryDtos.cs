using SportMate.Application.Sports;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// A court as shown to any authenticated user browsing courts. Unlike <see cref="CourtResponse"/> it carries
/// no management fields (owner, timestamps). Discovery only ever returns Active courts.
/// </summary>
public sealed record DiscoveredCourtResponse(
    Guid Id,
    string Name,
    string? Description,
    string Address,
    string City,
    decimal Latitude,
    decimal Longitude,
    string TimeZoneId,
    CourtStatus Status);

/// <summary>
/// A sport a court currently offers, with its hourly price. Only active court-sport links of active sports
/// are shown, so <paramref name="IsActive"/> is always true.
/// </summary>
public sealed record DiscoveredCourtSportResponse(
    SportResponse Sport,
    decimal PricePerHour,
    string Currency,
    bool IsActive);

/// <param name="DayOfWeek">0 = Sunday ... 6 = Saturday.</param>
/// <param name="OpensAt">Court-local time, "HH:mm".</param>
/// <param name="ClosesAt">Court-local time, "HH:mm".</param>
public sealed record DiscoveredOperatingHourResponse(int DayOfWeek, string OpensAt, string ClosesAt);
