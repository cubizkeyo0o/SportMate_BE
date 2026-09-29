using SportMate.Application.Common;
using SportMate.Application.Users;

namespace SportMate.UnitTests.Application;

public sealed class UserInputRulesTests
{
    [Fact]
    public void Email_IsTrimmedAndLowerCased()
    {
        var errors = new ValidationErrors();

        var email = UserInputRules.Email("  Minh.Anh@Example.COM ", errors);

        Assert.Equal("minh.anh@example.com", email);
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("user@localhost")]
    [InlineData("user@example.")]
    [InlineData("a b@example.com")]
    [InlineData("Name <user@example.com>")]
    public void Email_RejectsInvalidValues(string? value)
    {
        AssertInvalid(errors => UserInputRules.Email(value, errors), "email");
    }

    [Fact]
    public void Email_RejectsValuesLongerThanColumn()
    {
        var value = new string('a', 250) + "@example.com";

        AssertInvalid(errors => UserInputRules.Email(value, errors), "email");
    }

    [Theory]
    [InlineData("Passw0rd")]
    [InlineData("correct horse 7 battery")]
    public void NewPassword_AcceptsReasonablePasswords(string password)
    {
        var errors = new ValidationErrors();

        UserInputRules.NewPassword(password, errors);

        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sh0rt")]
    [InlineData("NoDigitsHere")]
    [InlineData("12345678")]
    public void NewPassword_RejectsWeakPasswords(string? password)
    {
        AssertInvalid(errors => UserInputRules.NewPassword(password, errors), "password");
    }

    [Fact]
    public void NewPassword_RejectsOverlyLongPasswords()
    {
        var password = new string('a', 128) + "1";

        AssertInvalid(errors => UserInputRules.NewPassword(password, errors), "password");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void DisplayName_IsRequired(string? value)
    {
        AssertInvalid(errors => UserInputRules.DisplayName(value, errors), "display_name");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData(" 0912345678 ", "0912345678")]
    [InlineData("+84 (912) 345-678", "+84 (912) 345-678")]
    public void Phone_IsOptionalAndTrimmed(string? value, string? expected)
    {
        var errors = new ValidationErrors();

        var phone = UserInputRules.Phone(value, errors);

        Assert.Equal(expected, phone);
        errors.ThrowIfAny();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0912-abc-678")]
    [InlineData("++84912345678")]
    [InlineData("1234567890123456")]
    public void Phone_RejectsInvalidValues(string value)
    {
        AssertInvalid(errors => UserInputRules.Phone(value, errors), "phone");
    }

    private static void AssertInvalid(Action<ValidationErrors> validate, string field)
    {
        var errors = new ValidationErrors();
        validate(errors);

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);
        Assert.True(exception.Errors.ContainsKey(field));
    }
}
