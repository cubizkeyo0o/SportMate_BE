using System.Net.Mail;
using SportMate.Application.Common;
using SportMate.Domain.Entities;

namespace SportMate.Application.Users;

/// <summary>
/// Validation and normalization for user-supplied account fields.
/// </summary>
internal static class UserInputRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    private const int PhoneMinDigits = 7;
    private const int PhoneMaxDigits = 15;

    /// <summary>
    /// Emails are compared and stored trimmed and lower-cased.
    /// </summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static string Email(string? value, ValidationErrors errors)
    {
        var email = NormalizeEmail(value);
        if (email.Length == 0)
        {
            errors.Add("email", "Email is required.");
        }
        else if (email.Length > User.EmailMaxLength)
        {
            errors.Add("email", $"Email must be at most {User.EmailMaxLength} characters.");
        }
        else if (!IsValidEmail(email))
        {
            errors.Add("email", "Email format is invalid.");
        }

        return email;
    }

    public static void NewPassword(string? password, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("password", "Password is required.");
            return;
        }

        if (password.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            errors.Add("password", $"Password must be between {PasswordMinLength} and {PasswordMaxLength} characters.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errors.Add("password", "Password must contain at least one letter and one digit.");
        }
    }

    public static string DisplayName(string? value, ValidationErrors errors)
    {
        var displayName = value?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            errors.Add("display_name", "Display name is required.");
        }
        else if (displayName.Length > User.DisplayNameMaxLength)
        {
            errors.Add("display_name", $"Display name must be at most {User.DisplayNameMaxLength} characters.");
        }

        return displayName;
    }

    public static string? Phone(string? value, ValidationErrors errors)
    {
        var phone = value?.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            return null;
        }

        var body = phone.StartsWith('+') ? phone[1..] : phone;
        var digits = body.Count(char.IsAsciiDigit);
        var allowedCharacters = body.All(c => char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' or '.');

        if (phone.Length > User.PhoneNumberMaxLength || !allowedCharacters || digits is < PhoneMinDigits or > PhoneMaxDigits)
        {
            errors.Add("phone", $"Phone must contain {PhoneMinDigits}-{PhoneMaxDigits} digits, optionally starting with '+', and be at most {User.PhoneNumberMaxLength} characters.");
        }

        return phone;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Any(char.IsWhiteSpace) || !MailAddress.TryCreate(email, out var address))
        {
            return false;
        }

        // Reject display-name forms such as "Name <a@b.com>" and single-label domains.
        return address.Address == email && address.Host.Contains('.') && !address.Host.EndsWith('.');
    }
}
