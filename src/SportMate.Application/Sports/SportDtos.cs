using SportMate.Domain.Enums;

namespace SportMate.Application.Sports;

/// <summary>
/// An active sport a user can pick, from <c>GET /api/v1/sports</c>.
/// </summary>
public sealed record SportResponse(Guid Id, string Name, string Slug);

/// <summary>
/// The sport of a profile. <paramref name="IsActive"/> is false when the sport was deactivated after
/// the profile was created.
/// </summary>
public sealed record ProfileSportResponse(Guid Id, string Name, string Slug, bool IsActive);

public sealed record UserSportProfileResponse(ProfileSportResponse Sport, SkillLevel SkillLevel);

/// <summary>
/// <paramref name="SkillLevel"/> accepts a name (<c>"beginner"</c>) or its stored number (1-5).
/// </summary>
public sealed record SetSportProfileRequest(SkillLevel? SkillLevel);
