using System.Globalization;
using System.Text.RegularExpressions;
using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.PlayRequests;

internal sealed record PlayRequestInput(
    Guid SportId,
    string Title,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    byte SpotsNeeded,
    decimal Latitude,
    decimal Longitude,
    SkillLevel? MinSkillLevel,
    SkillLevel? MaxSkillLevel,
    string? LocationName);

internal static partial class PlayRequestInputRules
{
    private const int CoordinateMaxDecimals = 6;

    public static PlayRequestInput Validate(
        Guid? sportId,
        string? title,
        string? description,
        string? startsAt,
        string? endsAt,
        byte? spotsNeeded,
        decimal? latitude,
        decimal? longitude,
        SkillLevel? minSkillLevel,
        SkillLevel? maxSkillLevel,
        string? locationName,
        DateTime utcNow,
        bool requireFutureStart = true)
    {
        var errors = new ValidationErrors();
        if (sportId is null || sportId == Guid.Empty)
        {
            errors.Add("sport_id", "Sport is required.");
        }

        var normalizedTitle = Required(title, PlayRequest.TitleMaxLength, "title", "Title", errors);
        var normalizedDescription = Optional(description, PlayRequest.DescriptionMaxLength, "description", "Description", errors);
        var normalizedLocation = Optional(locationName, PlayRequest.LocationNameMaxLength, "location_name", "Location name", errors);

        var start = UtcTimestamp(startsAt, "starts_at", errors);
        var end = UtcTimestamp(endsAt, "ends_at", errors);
        if (start is not null && end is not null && start >= end)
        {
            errors.Add("ends_at", "End time must be later than start time.");
        }

        if (requireFutureStart && start is not null && start <= utcNow)
        {
            errors.Add("starts_at", "Start time must be in the future.");
        }

        if (spotsNeeded is null or 0)
        {
            errors.Add("spots_needed", "Spots needed must be a positive number.");
        }

        var normalizedLatitude = Coordinate(latitude, 90, "latitude", "Latitude", errors);
        var normalizedLongitude = Coordinate(longitude, 180, "longitude", "Longitude", errors);

        if ((minSkillLevel is { } min && !Enum.IsDefined(min))
            || (maxSkillLevel is { } max && !Enum.IsDefined(max)))
        {
            errors.Add("min_skill_level", "Skill levels must be between beginner (1) and expert (5).");
        }
        else if (minSkillLevel > maxSkillLevel)
        {
            errors.Add("max_skill_level", "Maximum skill level cannot be lower than minimum skill level.");
        }

        errors.ThrowIfAny();
        return new PlayRequestInput(
            sportId!.Value,
            normalizedTitle,
            normalizedDescription,
            start!.Value,
            end!.Value,
            spotsNeeded!.Value,
            normalizedLatitude!.Value,
            normalizedLongitude!.Value,
            minSkillLevel,
            maxSkillLevel,
            normalizedLocation);
    }

    private static DateTime? UtcTimestamp(string? value, string field, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(field, "Timestamp is required.");
            return null;
        }

        if (!IsoWithOffset().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            errors.Add(field, "Timestamp must be ISO-8601 with 'Z' or an explicit UTC offset.");
            return null;
        }

        if (parsed.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            errors.Add(field, "Timestamp must use whole minutes; seconds and fractions of a second must be zero.");
            return null;
        }

        return parsed.UtcDateTime;
    }

    private static decimal? Coordinate(decimal? value, int limit, string field, string label, ValidationErrors errors)
    {
        if (value is null)
        {
            errors.Add(field, $"{label} is required.");
            return null;
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

    private static string Required(string? value, int maxLength, string field, string label, ValidationErrors errors)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            errors.Add(field, $"{label} is required.");
        }
        else if (normalized.Length > maxLength)
        {
            errors.Add(field, $"{label} must be at most {maxLength} characters.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string field, string label, ValidationErrors errors)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            errors.Add(field, $"{label} must be at most {maxLength} characters.");
        }

        return normalized;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex IsoWithOffset();
}
