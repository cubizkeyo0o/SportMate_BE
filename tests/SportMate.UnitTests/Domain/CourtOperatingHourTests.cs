using SportMate.Domain.Common;
using SportMate.Domain.Entities;

namespace SportMate.UnitTests.Domain;

public sealed class CourtOperatingHourTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(5);

    private static CourtOperatingHour Create(DayOfWeek day, string opensAt, string closesAt) =>
        new(Guid.NewGuid(), day, TimeOnly.Parse(opensAt), TimeOnly.Parse(closesAt), Now);

    [Fact]
    public void Constructor_SetsPeriodAndTimestamps()
    {
        var hour = Create(DayOfWeek.Monday, "06:00", "12:00");

        Assert.Equal(DayOfWeek.Monday, hour.DayOfWeek);
        Assert.Equal(new TimeOnly(6, 0), hour.OpensAt);
        Assert.Equal(new TimeOnly(12, 0), hour.ClosesAt);
        Assert.Equal(Now, hour.CreatedAtUtc);
        Assert.Equal(Now, hour.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("12:00", "12:00")]
    [InlineData("14:00", "12:00")]
    public void Constructor_RejectsOpeningNotBeforeClosing(string opensAt, string closesAt)
    {
        Assert.Throws<DomainException>(() => Create(DayOfWeek.Monday, opensAt, closesAt));
    }

    [Fact]
    public void Constructor_RejectsUndefinedDay()
    {
        Assert.Throws<DomainException>(() => Create((DayOfWeek)7, "06:00", "12:00"));
    }

    [Theory]
    [InlineData("06:00", "12:00", true)]  // identical
    [InlineData("11:00", "13:00", true)]  // overlaps the end
    [InlineData("05:00", "07:00", true)]  // overlaps the start
    [InlineData("08:00", "09:00", true)]  // inside
    [InlineData("05:00", "13:00", true)]  // contains
    [InlineData("12:00", "14:00", false)] // touches the end
    [InlineData("04:00", "06:00", false)] // touches the start
    [InlineData("14:00", "23:00", false)] // separate
    public void Overlaps_TreatsPeriodsAsHalfOpen(string opensAt, string closesAt, bool expected)
    {
        var existing = Create(DayOfWeek.Monday, "06:00", "12:00");

        Assert.Equal(expected, existing.Overlaps(DayOfWeek.Monday, TimeOnly.Parse(opensAt), TimeOnly.Parse(closesAt)));
    }

    [Fact]
    public void Overlaps_IgnoresOtherDays()
    {
        var existing = Create(DayOfWeek.Monday, "06:00", "12:00");

        Assert.False(existing.Overlaps(Create(DayOfWeek.Tuesday, "06:00", "12:00")));
    }

    [Fact]
    public void Reschedule_ReplacesPeriodAndTouchesTimestamp_OrIsNoOpWhenUnchanged()
    {
        var hour = Create(DayOfWeek.Monday, "06:00", "12:00");

        hour.Reschedule(DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(12, 0), Later);
        Assert.Equal(Now, hour.UpdatedAtUtc);

        hour.Reschedule(DayOfWeek.Sunday, new TimeOnly(14, 0), new TimeOnly(23, 0), Later);
        Assert.Equal(DayOfWeek.Sunday, hour.DayOfWeek);
        Assert.Equal(new TimeOnly(14, 0), hour.OpensAt);
        Assert.Equal(new TimeOnly(23, 0), hour.ClosesAt);
        Assert.Equal(Now, hour.CreatedAtUtc);
        Assert.Equal(Later, hour.UpdatedAtUtc);
    }

    [Fact]
    public void Reschedule_RejectsInvalidRange()
    {
        var hour = Create(DayOfWeek.Monday, "06:00", "12:00");

        Assert.Throws<DomainException>(() =>
            hour.Reschedule(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(6, 0), Later));
    }
}
