using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Domain;

public sealed class UserSportProfileTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(5);

    [Fact]
    public void SkillLevel_StoredValuesAreFixed()
    {
        Assert.Equal(1, (byte)SkillLevel.Beginner);
        Assert.Equal(2, (byte)SkillLevel.Elementary);
        Assert.Equal(3, (byte)SkillLevel.Intermediate);
        Assert.Equal(4, (byte)SkillLevel.Advanced);
        Assert.Equal(5, (byte)SkillLevel.Expert);
        Assert.Equal(5, Enum.GetValues<SkillLevel>().Length);
    }

    [Theory]
    [InlineData(SkillLevel.Beginner)]
    [InlineData(SkillLevel.Elementary)]
    [InlineData(SkillLevel.Intermediate)]
    [InlineData(SkillLevel.Advanced)]
    [InlineData(SkillLevel.Expert)]
    public void Constructor_AcceptsEveryDefinedSkillLevel(SkillLevel skillLevel)
    {
        var profile = new UserSportProfile(Guid.NewGuid(), Guid.NewGuid(), skillLevel, Now);

        Assert.Equal(skillLevel, profile.SkillLevel);
        Assert.Equal(Now, profile.CreatedAtUtc);
        Assert.Equal(Now, profile.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_RejectsUndefinedSkillLevel(byte value)
    {
        Assert.Throws<DomainException>(() =>
            new UserSportProfile(Guid.NewGuid(), Guid.NewGuid(), (SkillLevel)value, Now));
    }

    [Fact]
    public void ChangeSkillLevel_UpdatesValueAndTimestamp()
    {
        var profile = new UserSportProfile(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Beginner, Now);

        profile.ChangeSkillLevel(SkillLevel.Advanced, Later);

        Assert.Equal(SkillLevel.Advanced, profile.SkillLevel);
        Assert.Equal(Now, profile.CreatedAtUtc);
        Assert.Equal(Later, profile.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeSkillLevel_ToSameValue_IsANoOp()
    {
        var profile = new UserSportProfile(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Intermediate, Now);

        profile.ChangeSkillLevel(SkillLevel.Intermediate, Later);

        Assert.Equal(Now, profile.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeSkillLevel_RejectsUndefinedSkillLevel()
    {
        var profile = new UserSportProfile(Guid.NewGuid(), Guid.NewGuid(), SkillLevel.Beginner, Now);

        Assert.Throws<DomainException>(() => profile.ChangeSkillLevel((SkillLevel)9, Later));
        Assert.Equal(SkillLevel.Beginner, profile.SkillLevel);
    }
}
