using SportMate.Application.Common;
using SportMate.Application.PlayRequests;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Application;

public sealed class PlayRequestInputRulesTests
{
    [Fact]
    public void Validate_ConvertsExplicitOffsetToUtcAndAllowsUnboundedSkillRange()
    {
        var now = DateTime.UtcNow;
        var localStart = DateTime.SpecifyKind(RoundToMinute(now.AddDays(2)), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(localStart, TimeSpan.FromHours(7));
        var end = start.AddHours(1);

        var input = Validate(start.ToString("O"), end.ToString("O"), minSkillLevel: null, maxSkillLevel: null, now);

        Assert.Equal(start.UtcDateTime, input.StartsAtUtc);
        Assert.Equal(end.UtcDateTime, input.EndsAtUtc);
        Assert.Null(input.MinSkillLevel);
        Assert.Null(input.MaxSkillLevel);
    }

    [Theory]
    [InlineData(SkillLevel.Beginner, null)]
    [InlineData(null, SkillLevel.Expert)]
    [InlineData(SkillLevel.Elementary, SkillLevel.Advanced)]
    public void Validate_AcceptsOptionalSkillBounds(SkillLevel? min, SkillLevel? max)
    {
        var now = DateTime.UtcNow;
        var start = new DateTimeOffset(RoundToMinute(now.AddDays(1)), TimeSpan.Zero);

        var input = Validate(start.ToString("O"), start.AddHours(1).ToString("O"), min, max, now);

        Assert.Equal(min, input.MinSkillLevel);
        Assert.Equal(max, input.MaxSkillLevel);
    }

    [Fact]
    public void Validate_RejectsStartNotInFuture()
    {
        var now = DateTime.UtcNow;
        var start = new DateTimeOffset(now, TimeSpan.Zero);

        var exception = Assert.Throws<ValidationException>(() =>
            Validate(start.ToString("O"), start.AddHours(1).ToString("O"), null, null, now));

        Assert.Contains("starts_at", exception.Errors.Keys);
    }

    [Fact]
    public void Validate_RejectsInvalidSkillBounds()
    {
        var now = DateTime.UtcNow;
        var start = new DateTimeOffset(RoundToMinute(now.AddDays(1)), TimeSpan.Zero);

        var exception = Assert.Throws<ValidationException>(() =>
            Validate(start.ToString("O"), start.AddHours(1).ToString("O"), SkillLevel.Expert, SkillLevel.Beginner, now));

        Assert.Contains("max_skill_level", exception.Errors.Keys);
    }

    [Fact]
    public void Validate_RejectsInvalidCoordinatesAndZeroSpots()
    {
        var now = DateTime.UtcNow;
        var start = new DateTimeOffset(RoundToMinute(now.AddDays(1)), TimeSpan.Zero);
        var exception = Assert.Throws<ValidationException>(() => PlayRequestInputRules.Validate(
            Guid.NewGuid(), "Game", null, start.ToString("O"), start.AddHours(1).ToString("O"), 0, 91, 181,
            null, null, null, now));

        Assert.Contains("spots_needed", exception.Errors.Keys);
        Assert.Contains("latitude", exception.Errors.Keys);
        Assert.Contains("longitude", exception.Errors.Keys);
    }

    [Fact]
    public void Validate_RejectsTimestampsWithoutExplicitOffset()
    {
        var now = DateTime.UtcNow;
        var start = RoundToMinute(now.AddDays(1)).ToString("yyyy-MM-dd'T'HH:mm:ss");
        var end = RoundToMinute(now.AddDays(1).AddHours(1)).ToString("yyyy-MM-dd'T'HH:mm:ss");

        var exception = Assert.Throws<ValidationException>(() => Validate(start, end, null, null, now));

        Assert.Contains("starts_at", exception.Errors.Keys);
        Assert.Contains("ends_at", exception.Errors.Keys);
    }

    private static PlayRequestInput Validate(
        string start,
        string end,
        SkillLevel? minSkillLevel,
        SkillLevel? maxSkillLevel,
        DateTime now) =>
        PlayRequestInputRules.Validate(
            Guid.NewGuid(), "Game", null, start, end, 2, 21.028511m, 105.804817m,
            minSkillLevel, maxSkillLevel, null, now);

    private static DateTime RoundToMinute(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, value.Kind);
}
