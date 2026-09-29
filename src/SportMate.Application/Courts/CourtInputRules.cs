using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// Validation and normalization for user-supplied court fields.
/// </summary>
internal static class CourtInputRules
{
    // courts.latitude / courts.longitude are DECIMAL(9,6).
    private const int CoordinateMaxDecimals = 6;

    public static string Name(string? value, ValidationErrors errors) =>
        Required(value, Court.NameMaxLength, "name", "Name", errors);

    public static string? Description(string? value, ValidationErrors errors)
    {
        var description = value?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            return null;
        }

        if (description.Length > Court.DescriptionMaxLength)
        {
            errors.Add("description", $"Description must be at most {Court.DescriptionMaxLength} characters.");
        }

        return description;
    }

    public static string Address(string? value, ValidationErrors errors) =>
        Required(value, Court.AddressMaxLength, "address", "Address", errors);

    public static string City(string? value, ValidationErrors errors) =>
        Required(value, Court.CityMaxLength, "city", "City", errors);

    public static decimal Latitude(decimal? value, ValidationErrors errors) =>
        Coordinate(value, 90, "latitude", "Latitude", errors);

    public static decimal Longitude(decimal? value, ValidationErrors errors) =>
        Coordinate(value, 180, "longitude", "Longitude", errors);

    /// <summary>
    /// Accepts only IANA identifiers (e.g. "Asia/Ho_Chi_Minh") known to the runtime; Windows
    /// identifiers such as "SE Asia Standard Time" are rejected.
    /// </summary>
    public static string TimeZoneId(string? value, ValidationErrors errors)
    {
        var timeZoneId = Required(value, Court.TimeZoneIdMaxLength, "time_zone_id", "Time zone", errors);
        if (timeZoneId.Length is > 0 and <= Court.TimeZoneIdMaxLength
            && !(TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _)
                 && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _)))
        {
            errors.Add("time_zone_id", "Time zone must be a valid IANA time zone identifier, e.g. \"Asia/Ho_Chi_Minh\".");
        }

        return timeZoneId;
    }

    public static CourtStatus Status(CourtStatus? value, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add("status", "Status is required.");
            return default;
        }

        if (!Enum.IsDefined(value.Value))
        {
            errors.Add("status", "Status must be \"active\" or \"inactive\".");
        }

        return value.Value;
    }

    private static string Required(string? value, int maxLength, string field, string label, ValidationErrors errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors.Add(field, $"{label} is required.");
        }
        else if (trimmed.Length > maxLength)
        {
            errors.Add(field, $"{label} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    private static decimal Coordinate(decimal? value, int limit, string field, string label, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add(field, $"{label} is required.");
            return default;
        }

        if (value < -limit || value > limit)
        {
            errors.Add(field, $"{label} must be between -{limit} and {limit}.");
        }
        else if (decimal.Round(value.Value, CoordinateMaxDecimals) != value)
        {
            errors.Add(field, $"{label} must have at most {CoordinateMaxDecimals} decimal places.");
        }

        return value.Value;
    }
}
