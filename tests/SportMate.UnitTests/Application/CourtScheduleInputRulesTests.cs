using SportMate.Application.Common;
using SportMate.Application.Courts;

namespace SportMate.UnitTests.Application;

public sealed class CourtScheduleInputRulesTests
{
    // ---- court sports ----

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(150000)]
    [InlineData(99999999.99)]
    public void PricePerHour_AcceptsZeroAndPositiveMoney(double value)
    {
        var errors = new ValidationErrors();

        CourtSportInputRules.PricePerHour((decimal)value, errors);

        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(-0.01)]
    [InlineData(1.001)]
    [InlineData(100000000.0)]
    public void PricePerHour_RejectsMissingNegativeTooPreciseOrTooLargeValues(double? value)
    {
        AssertInvalid(errors => CourtSportInputRules.PricePerHour((decimal?)value, errors), "price_per_hour");
    }

    [Fact]
    public void Currency_IsTrimmedAndUppercased()
    {
        var errors = new ValidationErrors();

        Assert.Equal("VND", CourtSportInputRules.Currency(" vnd ", errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("VN")]
    [InlineData("VNDX")]
    [InlineData("V1D")]
    [InlineData("đồng")]
    public void Currency_RejectsAnythingButThreeLetters(string? value)
    {
        AssertInvalid(errors => CourtSportInputRules.Currency(value, errors), "currency");
    }

    [Fact]
    public void IsActive_IsRequired()
    {
        AssertInvalid(errors => CourtSportInputRules.IsActive(null, errors), "is_active");
    }

    // ---- operating hours ----

    [Theory]
    [InlineData(0, DayOfWeek.Sunday)]
    [InlineData(1, DayOfWeek.Monday)]
    [InlineData(6, DayOfWeek.Saturday)]
    public void DayOfWeek_AcceptsZeroToSix(int value, DayOfWeek expected)
    {
        var errors = new ValidationErrors();

        Assert.Equal(expected, CourtOperatingHourInputRules.DayOfWeek(value, errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(7)]
    public void DayOfWeek_RejectsMissingOrOutOfRange(int? value)
    {
        AssertInvalid(errors => CourtOperatingHourInputRules.DayOfWeek(value, errors), "day_of_week");
    }

    [Theory]
    [InlineData("00:00", 0, 0)]
    [InlineData("06:30", 6, 30)]
    [InlineData("23:59", 23, 59)]
    public void Time_AcceptsHoursAndMinutes(string value, int hour, int minute)
    {
        var errors = new ValidationErrors();

        Assert.Equal(new TimeOnly(hour, minute), CourtOperatingHourInputRules.Time(value, "opens_at", "Opening time", errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24:00")]
    [InlineData("06:00:00")]
    [InlineData("06:00:30")]
    [InlineData("25:00")]
    [InlineData("06:60")]
    [InlineData("6am")]
    [InlineData(" 06:00")]
    public void Time_RejectsMissingSecondsMidnightAndMalformedValues(string? value)
    {
        AssertInvalid(errors => CourtOperatingHourInputRules.Time(value, "opens_at", "Opening time", errors), "opens_at");
    }

    [Theory]
    [InlineData("12:00", "12:00")]
    [InlineData("14:00", "12:00")]
    [InlineData("22:00", "02:00")]
    public void Period_RejectsOpeningNotBeforeClosing(string opensAt, string closesAt)
    {
        AssertInvalid(
            errors => CourtOperatingHourInputRules.Period(new OperatingHourRequest(1, opensAt, closesAt), errors),
            "closes_at");
    }

    [Fact]
    public void Format_UsesHoursAndMinutes()
    {
        Assert.Equal("06:05", CourtOperatingHourInputRules.Format(new TimeOnly(6, 5)));
    }

    private static void AssertInvalid(Action<ValidationErrors> validate, string field)
    {
        var errors = new ValidationErrors();
        validate(errors);

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        Assert.True(exception.Errors.ContainsKey(field));
    }
}
