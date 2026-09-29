using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

public sealed class UserSportProfile : Entity
{
    private UserSportProfile()
    {
    }

    public UserSportProfile(Guid userId, Guid sportId, SkillLevel skillLevel, DateTime utcNow)
    {
        UserId = userId;
        SportId = sportId;
        SkillLevel = ValidSkill(skillLevel);
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public Guid SportId { get; private set; }

    public Sport Sport { get; private set; } = null!;

    public SkillLevel SkillLevel { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Setting the current skill level is a no-op.
    /// </summary>
    public void ChangeSkillLevel(SkillLevel skillLevel, DateTime utcNow)
    {
        ValidSkill(skillLevel);
        Guard.Utc(utcNow, nameof(utcNow));
        if (skillLevel == SkillLevel)
        {
            return;
        }

        SkillLevel = skillLevel;
        UpdatedAtUtc = utcNow;
    }

    private static SkillLevel ValidSkill(SkillLevel skillLevel) =>
        Enum.IsDefined(skillLevel) ? skillLevel : throw new DomainException("Invalid skill level.");
}
