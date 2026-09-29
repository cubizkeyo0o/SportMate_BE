namespace SportMate.Domain.Enums;

/// <summary>
/// Values are persisted in user_sport_profiles.skill_level and play_requests and must not change.
/// </summary>
public enum SkillLevel : byte
{
    Beginner = 1,
    Elementary = 2,
    Intermediate = 3,
    Advanced = 4,
    Expert = 5
}
