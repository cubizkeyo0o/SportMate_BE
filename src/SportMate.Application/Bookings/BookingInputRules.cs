using System.Globalization;
using System.Text.RegularExpressions;
using SportMate.Application.Common;
using SportMate.Domain.Entities;

namespace SportMate.Application.Bookings;

/// <summary>
/// Validation and parsing for user-supplied booking fields.
/// </summary>
internal static partial class BookingInputRules
{
    /// <summary>
    /// Parses an ISO-8601 timestamp that carries <c>Z</c> or an explicit offset and has no seconds or
    /// fractions other than zero. Returns the UTC instant.
    /// </summary>
    public static DateTime? Timestamp(string? value, string field, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(field, "Timestamp is required.");
            return null;
        }

        if (!IsoWithOffset().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            errors.Add(
                field,
                "Timestamp must be ISO-8601 with 'Z' or an explicit offset, e.g. \"2026-10-01T10:00:00+07:00\".");
            return null;
        }

        if (parsed.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            errors.Add(field, "Bookings use whole minutes; seconds and fractions of a second must be zero.");
            return null;
        }

        return parsed.UtcDateTime;
    }

    public static string? CancellationReason(string? value, ValidationErrors errors)
    {
        var reason = value?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return null;
        }

        if (reason.Length > Booking.CancellationReasonMaxLength)
        {
            errors.Add("reason", $"Reason must be at most {Booking.CancellationReasonMaxLength} characters.");
        }

        return reason;
    }

    // Date, 'T', hh:mm, optional :ss and fraction, then Z or ±hh:mm. Rejects timezone-less values.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex IsoWithOffset();
}
