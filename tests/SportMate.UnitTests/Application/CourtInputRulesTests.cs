using SportMate.Application.Common;
using SportMate.Application.Courts;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Application;

public sealed class CourtInputRulesTests
{
    [Fact]
    public void TextFields_AreTrimmed_AndBlankDescriptionBecomesNull()
    {
        var errors = new ValidationErrors();

        Assert.Equal("Court A", CourtInputRules.Name("  Court A ", errors));
        Assert.Equal("Ha Noi", CourtInputRules.City(" Ha Noi ", errors));
        Assert.Null(CourtInputRules.Description("   ", errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void RequiredTextFields_RejectMissingValues(string? value)
    {
        AssertInvalid(errors => CourtInputRules.Name(value, errors), "name");
        AssertInvalid(errors => CourtInputRules.Address(value, errors), "address");
        AssertInvalid(errors => CourtInputRules.City(value, errors), "city");
        AssertInvalid(errors => CourtInputRules.TimeZoneId(value, errors), "time_zone_id");
    }

    [Fact]
    public void TextFields_RejectValuesLongerThanColumns()
    {
        AssertInvalid(errors => CourtInputRules.Name(new string('a', 151), errors), "name");
        AssertInvalid(errors => CourtInputRules.Description(new string('a', 1001), errors), "description");
        AssertInvalid(errors => CourtInputRules.Address(new string('a', 301), errors), "address");
        AssertInvalid(errors => CourtInputRules.City(new string('a', 101), errors), "city");
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(21.028511, 105.804817)]
    public void Coordinates_AcceptValuesWithinRangeAndScale(double latitude, double longitude)
    {
        var errors = new ValidationErrors();

        CourtInputRules.Latitude((decimal)latitude, errors);
        CourtInputRules.Longitude((decimal)longitude, errors);

        errors.ThrowIfAny();
    }

    [Fact]
    public void Coordinates_RejectMissingOutOfRangeAndTooPreciseValues()
    {
        AssertInvalid(errors => CourtInputRules.Latitude(null, errors), "latitude");
        AssertInvalid(errors => CourtInputRules.Latitude(90.000001m, errors), "latitude");
        AssertInvalid(errors => CourtInputRules.Latitude(-91m, errors), "latitude");
        AssertInvalid(errors => CourtInputRules.Latitude(21.0285111m, errors), "latitude");
        AssertInvalid(errors => CourtInputRules.Longitude(null, errors), "longitude");
        AssertInvalid(errors => CourtInputRules.Longitude(180.5m, errors), "longitude");
        AssertInvalid(errors => CourtInputRules.Longitude(-181m, errors), "longitude");
    }

    [Theory]
    [InlineData("Asia/Ho_Chi_Minh")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    public void TimeZoneId_AcceptsIanaIdentifiers(string value)
    {
        var errors = new ValidationErrors();

        Assert.Equal(value, CourtInputRules.TimeZoneId(value, errors));
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData("Not/A_Zone")]
    [InlineData("SE Asia Standard Time")]
    [InlineData("GMT+7")]
    public void TimeZoneId_RejectsUnknownAndWindowsIdentifiers(string value)
    {
        AssertInvalid(errors => CourtInputRules.TimeZoneId(value, errors), "time_zone_id");
    }

    [Theory]
    [InlineData(CourtStatus.Active)]
    [InlineData(CourtStatus.Inactive)]
    public void Status_AcceptsDefinedValues(CourtStatus status)
    {
        var errors = new ValidationErrors();

        Assert.Equal(status, CourtInputRules.Status(status, errors));
        errors.ThrowIfAny();
    }

    [Fact]
    public void Status_RejectsMissingAndUndefinedValues()
    {
        AssertInvalid(errors => CourtInputRules.Status(null, errors), "status");
        AssertInvalid(errors => CourtInputRules.Status((CourtStatus)3, errors), "status");
    }

    private static void AssertInvalid(Action<ValidationErrors> validate, string field)
    {
        var errors = new ValidationErrors();
        validate(errors);

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        Assert.True(exception.Errors.ContainsKey(field));
    }
}
