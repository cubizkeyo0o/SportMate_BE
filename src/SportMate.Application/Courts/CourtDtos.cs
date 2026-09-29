using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

public sealed record CourtResponse(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Description,
    string Address,
    string City,
    decimal Latitude,
    decimal Longitude,
    string TimeZoneId,
    CourtStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static CourtResponse From(Court court) =>
        new(
            court.Id,
            court.OwnerId,
            court.Name,
            court.Description,
            court.Address,
            court.City,
            court.Latitude,
            court.Longitude,
            court.TimeZoneId,
            court.Status,
            court.CreatedAtUtc,
            court.UpdatedAtUtc);
}

/// <summary>
/// Admin only. <paramref name="OwnerId"/> must be an active user with the CourtOwner role.
/// New courts start <see cref="CourtStatus.Active"/>.
/// </summary>
public sealed record CreateCourtRequest(
    Guid? OwnerId,
    string? Name,
    string? Description,
    string? Address,
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    string? TimeZoneId);

/// <summary>
/// Full replacement of the editable court fields; omitting description clears it.
/// Omitting <paramref name="OwnerId"/> keeps the current owner; only Admin may change it.
/// </summary>
public sealed record UpdateCourtRequest(
    string? Name,
    string? Description,
    string? Address,
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    string? TimeZoneId,
    CourtStatus? Status,
    Guid? OwnerId);
