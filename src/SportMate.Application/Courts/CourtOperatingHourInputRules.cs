using System.Globalization;
using SportMate.Application.Common;

namespace SportMate.Application.Courts;

/// <summary>
/// Validation and parsing for user-supplied operating-hour fields.
/// </summary>
internal static class CourtOperatingHourInputRules
{
    private const string TimeFormat = "HH:mm";

    public static string Format(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static DayOfWeek DayOfWeek(int? value, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add("day_of_week", "Day of week is required.");
            return default;
        }

        if (value is < 0 or > 6)
        {
            errors.Add("day_of_week", "Day of week must be 0 (Sunday) to 6 (Saturday).");
            return default;
        }

        return (DayOfWeek)value.Value;
    }

    /// <summary>
    /// Exactly "HH:mm" from 00:00 to 23:59. Seconds and 24:00 are rejected.
    /// </summary>
    public static TimeOnly? Time(string? value, string field, string label, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(field, $"{label} is required.");
            return null;
        }

        if (!TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            errors.Add(field, $"{label} must be a time in HH:mm format from 00:00 to 23:59 (24:00 is not supported).");
            return null;
        }

        return time;
    }

    public static (DayOfWeek Day, TimeOnly OpensAt, TimeOnly ClosesAt) Period(
        OperatingHourRequest request,
        ValidationErrors errors)
    {
        var day = DayOfWeek(request.DayOfWeek, errors);
        var opensAt = Time(request.OpensAt, "opens_at", "Opening time", errors);
        var closesAt = Time(request.ClosesAt, "closes_at", "Closing time", errors);
        if (opensAt is not null && closesAt is not null && opensAt >= closesAt)
        {
            errors.Add("closes_at", "Closing time must be later than opening time; a period cannot cross midnight.");
        }

        return (day, opensAt ?? default, closesAt ?? default);
    }
}
