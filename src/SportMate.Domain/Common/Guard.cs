namespace SportMate.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, string name)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NotBlank(value, maxLength, name);
    }

    public static DateTime Utc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException($"{name} must be a UTC timestamp.");
        }

        return value;
    }

    public static void UtcRange(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        Utc(startsAtUtc, nameof(startsAtUtc));
        Utc(endsAtUtc, nameof(endsAtUtc));
        if (startsAtUtc >= endsAtUtc)
        {
            throw new DomainException("Start time must be earlier than end time.");
        }
    }

    public static string Currency(string? value)
    {
        if (value is null || value.Length != 3 || !value.All(char.IsAsciiLetter))
        {
            throw new DomainException("Currency must be a 3-letter ISO 4217 code.");
        }

        return value.ToUpperInvariant();
    }

    public static decimal Money(decimal value, string name, bool allowZero)
    {
        if (value < 0 || (!allowZero && value == 0))
        {
            throw new DomainException($"{name} must be {(allowZero ? "zero or positive" : "positive")}.");
        }

        if (decimal.Round(value, 2) != value || value > 99_999_999.99m)
        {
            throw new DomainException($"{name} must fit DECIMAL(10,2).");
        }

        return value;
    }

    public static void Coordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }
    }
}
