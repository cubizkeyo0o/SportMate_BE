using SportMate.Domain.Entities;

namespace SportMate.Application.Courts;

/// <param name="DayOfWeek">0 = Sunday ... 6 = Saturday.</param>
/// <param name="OpensAt">Court-local time, "HH:mm".</param>
/// <param name="ClosesAt">Court-local time, "HH:mm"; later than <paramref name="OpensAt"/>.</param>
public sealed record OperatingHourResponse(
    Guid Id,
    int DayOfWeek,
    string OpensAt,
    string ClosesAt,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static OperatingHourResponse From(CourtOperatingHour hour) =>
        new(
            hour.Id,
            (int)hour.DayOfWeek,
            CourtOperatingHourInputRules.Format(hour.OpensAt),
            CourtOperatingHourInputRules.Format(hour.ClosesAt),
            hour.CreatedAtUtc,
            hour.UpdatedAtUtc);
}

/// <summary>
/// Used to add a period and, as a full replacement, to update one. Times are "HH:mm" (00:00-23:59).
/// </summary>
public sealed record OperatingHourRequest(int? DayOfWeek, string? OpensAt, string? ClosesAt);
