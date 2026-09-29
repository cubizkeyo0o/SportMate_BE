using SportMate.Application.Common;

namespace SportMate.Application.Courts;

/// <summary>
/// Validation and normalization for user-supplied court-sport fields.
/// </summary>
internal static class CourtSportInputRules
{
    // court_sports.price_per_hour is DECIMAL(10,2).
    public const decimal MaxPrice = 99_999_999.99m;

    public static decimal PricePerHour(decimal? value, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add("price_per_hour", "Price per hour is required.");
            return default;
        }

        if (value < 0)
        {
            errors.Add("price_per_hour", "Price per hour must be zero or positive.");
        }
        else if (value > MaxPrice)
        {
            errors.Add("price_per_hour", $"Price per hour must be at most {MaxPrice}.");
        }
        else if (decimal.Round(value.Value, 2) != value)
        {
            errors.Add("price_per_hour", "Price per hour must have at most 2 decimal places.");
        }

        return value.Value;
    }

    /// <summary>
    /// A 3-letter ISO 4217 code, stored uppercase (e.g. "VND").
    /// </summary>
    public static string Currency(string? value, ValidationErrors errors)
    {
        var currency = value?.Trim() ?? string.Empty;
        if (currency.Length == 0)
        {
            errors.Add("currency", "Currency is required.");
        }
        else if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
        {
            errors.Add("currency", "Currency must be a 3-letter ISO 4217 code, e.g. \"VND\".");
        }

        return currency.ToUpperInvariant();
    }

    public static bool IsActive(bool? value, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add("is_active", "is_active is required.");
            return default;
        }

        return value.Value;
    }
}
