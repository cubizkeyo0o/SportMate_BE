using SportMate.Domain.Enums;
using System.Text.Json.Serialization;

namespace SportMate.Application.PlayRequests;

/// <summary>Creates a request for additional players using ISO-8601 timestamps with UTC or an explicit offset.</summary>
public sealed record CreatePlayRequestRequest(
    Guid? SportId,
    string? Title,
    string? Description,
    string? StartsAt,
    string? EndsAt,
    byte? SpotsNeeded,
    decimal? Latitude,
    decimal? Longitude,
    SkillLevel? MinSkillLevel,
    SkillLevel? MaxSkillLevel,
    string? LocationName);

/// <summary>Replaces the host-editable fields of a request before anyone has requested to join.</summary>
public sealed record UpdatePlayRequestRequest(
    Guid? SportId,
    string? Title,
    string? Description,
    string? StartsAt,
    string? EndsAt,
    byte? SpotsNeeded,
    decimal? Latitude,
    decimal? Longitude,
    SkillLevel? MinSkillLevel,
    SkillLevel? MaxSkillLevel,
    string? LocationName);

/// <summary>Filters eligible open and full requests. Radius searches require all three location fields.</summary>
public sealed record PlayRequestQuery(
    Guid? SportId,
    DateTimeOffset? StartsFromUtc,
    DateTimeOffset? StartsToUtc,
    decimal? Latitude,
    decimal? Longitude,
    double? RadiusKm,
    int Page = 1,
    int PageSize = 20);

/// <summary>One eligible player who has requested to join, visible only to the host.</summary>
public sealed record PlayRequestParticipantResponse(
    Guid Id,
    string DisplayName,
    SkillLevel? SkillLevel,
    ParticipantStatus Status,
    DateTimeOffset RequestedAtUtc);

/// <summary>A privacy-limited summary of the request host.</summary>
public sealed record PlayRequestHostResponse(string DisplayName, SkillLevel? SkillLevel);

/// <summary>A player-facing request; exact coordinates and private contact details are never returned.</summary>
public sealed record PlayRequestResponse(
    Guid Id,
    Guid SportId,
    string SportName,
    string SportSlug,
    string Title,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    byte SpotsNeeded,
    int AcceptedCount,
    string? LocationName,
    SkillLevel? MinSkillLevel,
    SkillLevel? MaxSkillLevel,
    PlayRequestStatus Status,
    PlayRequestHostResponse Host,
    ParticipantStatus? MyParticipantStatus,
    IReadOnlyList<PlayRequestParticipantResponse> Participants,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Latitude,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Longitude,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>A page of matching requests.</summary>
public sealed record PlayRequestPageResponse(
    IReadOnlyList<PlayRequestResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>A page of the host-visible participant requests for one play request.</summary>
public sealed record PlayRequestParticipantPageResponse(
    IReadOnlyList<PlayRequestParticipantResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
