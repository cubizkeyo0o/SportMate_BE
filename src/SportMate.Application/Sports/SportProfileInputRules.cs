using SportMate.Application.Common;
using SportMate.Domain.Enums;

namespace SportMate.Application.Sports;

/// <summary>
/// Validation for user-supplied sport profile fields.
/// </summary>
internal static class SportProfileInputRules
{
    public static SkillLevel SkillLevel(SkillLevel? value, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add("skill_level", "Skill level is required.");
            return default;
        }

        if (!Enum.IsDefined(value.Value))
        {
            errors.Add(
                "skill_level",
                "Skill level must be one of beginner (1), elementary (2), intermediate (3), advanced (4), expert (5).");
        }

        return value.Value;
    }
}
