using SportMate.Application.Common;
using SportMate.Application.Sports;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Application;

public sealed class SportProfileInputRulesTests
{
    [Theory]
    [InlineData(SkillLevel.Beginner)]
    [InlineData(SkillLevel.Elementary)]
    [InlineData(SkillLevel.Intermediate)]
    [InlineData(SkillLevel.Advanced)]
    [InlineData(SkillLevel.Expert)]
    public void SkillLevel_AcceptsDefinedValues(SkillLevel value)
    {
        var errors = new ValidationErrors();

        Assert.Equal(value, SportProfileInputRules.SkillLevel(value, errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    [InlineData((byte)255)]
    public void SkillLevel_RejectsMissingAndUndefinedValues(byte? value)
    {
        var errors = new ValidationErrors();
        SportProfileInputRules.SkillLevel((SkillLevel?)value, errors);

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        Assert.True(exception.Errors.ContainsKey("skill_level"));
    }
}
