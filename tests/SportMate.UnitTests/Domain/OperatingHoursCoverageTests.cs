using SportMate.Domain.Entities;

namespace SportMate.UnitTests.Domain;

public sealed class OperatingHoursCoverageTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);

    private static CourtOperatingHour Period(string opensAt, string closesAt) =>
        new(Guid.Empty, DayOfWeek.Monday, TimeOnly.Parse(opensAt), TimeOnly.Parse(closesAt), Now);

    private static readonly CourtOperatingHour[] WithGap = [Period("13:00", "18:00"), Period("09:00", "12:00")];

    [Theory]
    [InlineData("10:00", "11:00", true)]  // inside one period
    [InlineData("09:00", "12:00", true)]  // exactly one period
    [InlineData("13:00", "18:00", true)]
    [InlineData("11:30", "13:30", false)] // crosses the 12:00-13:00 gap
    [InlineData("12:00", "13:00", false)] // only the gap
    [InlineData("08:30", "09:30", false)] // starts before opening
    [InlineData("17:30", "18:30", false)] // ends after closing
    [InlineData("11:00", "11:00", false)] // empty
    public void Covers_RequiresTheWholeRangeInsideTheUnion(string from, string to, bool expected)
    {
        Assert.Equal(expected, CourtOperatingHour.Covers(WithGap, TimeOnly.Parse(from), TimeOnly.Parse(to)));
    }

    [Fact]
    public void Covers_TreatsTouchingPeriodsAsContinuous()
    {
        CourtOperatingHour[] touching = [Period("06:00", "12:00"), Period("12:00", "14:00"), Period("14:00", "16:00")];

        Assert.True(CourtOperatingHour.Covers(touching, new TimeOnly(11, 0), new TimeOnly(15, 0)));
        Assert.True(CourtOperatingHour.Covers(touching, new TimeOnly(6, 0), new TimeOnly(16, 0)));
        Assert.False(CourtOperatingHour.Covers(touching, new TimeOnly(15, 0), new TimeOnly(16, 30)));
    }

    [Fact]
    public void Covers_WithNoPeriods_IsFalse()
    {
        Assert.False(CourtOperatingHour.Covers([], new TimeOnly(10, 0), new TimeOnly(11, 0)));
    }
}
