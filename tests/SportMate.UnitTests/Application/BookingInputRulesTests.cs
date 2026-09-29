using SportMate.Application.Bookings;
using SportMate.Application.Common;

namespace SportMate.UnitTests.Application;

public sealed class BookingInputRulesTests
{
    [Theory]
    [InlineData("2026-10-01T10:00:00Z", "2026-10-01T10:00:00")]
    [InlineData("2026-10-01T10:00Z", "2026-10-01T10:00:00")]
    [InlineData("2026-10-01T10:00:00+07:00", "2026-10-01T03:00:00")]
    [InlineData("2026-10-01T10:00:00-05:30", "2026-10-01T15:30:00")]
    [InlineData("2026-10-01T10:00:00.000Z", "2026-10-01T10:00:00")]
    [InlineData("2026-10-01T01:00:00+07:00", "2026-09-30T18:00:00")] // previous UTC day
    public void Timestamp_AcceptsZOrOffset_AndReturnsUtc(string value, string expectedUtc)
    {
        var errors = new ValidationErrors();

        var parsed = BookingInputRules.Timestamp(value, "starts_at", errors);

        errors.ThrowIfAny();
        Assert.Equal(DateTime.SpecifyKind(DateTime.Parse(expectedUtc), DateTimeKind.Utc), parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-10-01T10:00:00")]      // no time zone
    [InlineData("2026-10-01 10:00:00Z")]     // no 'T'
    [InlineData("2026-10-01")]
    [InlineData("10:00Z")]
    [InlineData("2026-10-01T10:00:00+0700")] // offset without colon
    [InlineData("2026-13-01T10:00:00Z")]     // invalid month
    [InlineData("tomorrow")]
    public void Timestamp_RejectsMissingMalformedOrTimezoneLessValues(string? value)
    {
        AssertInvalid(errors => BookingInputRules.Timestamp(value, "starts_at", errors), "starts_at");
    }

    [Theory]
    [InlineData("2026-10-01T10:00:30Z")]
    [InlineData("2026-10-01T10:30:15+07:00")]
    [InlineData("2026-10-01T10:00:00.5Z")]
    [InlineData("2026-10-01T10:00:00.0000001Z")]
    public void Timestamp_RejectsSecondsAndFractions(string value)
    {
        AssertInvalid(errors => BookingInputRules.Timestamp(value, "ends_at", errors), "ends_at");
    }

    [Fact]
    public void CancellationReason_IsOptionalTrimmedAndLimited()
    {
        var errors = new ValidationErrors();

        Assert.Null(BookingInputRules.CancellationReason("   ", errors));
        Assert.Equal("rain", BookingInputRules.CancellationReason(" rain ", errors));
        errors.ThrowIfAny();

        AssertInvalid(errors => BookingInputRules.CancellationReason(new string('r', 501), errors), "reason");
    }

    private static void AssertInvalid(Action<ValidationErrors> validate, string field)
    {
        var errors = new ValidationErrors();
        validate(errors);

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        Assert.True(exception.Errors.ContainsKey(field));
    }
}
