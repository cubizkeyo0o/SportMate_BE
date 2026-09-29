using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

/// <summary>
/// One weekly opening period of a court on a day of the week, in the court's local time
/// (<see cref="Court.TimeZoneId"/>). A day may have several periods; they must not overlap, but may touch
/// (06:00-12:00 and 12:00-14:00). A period cannot close at 24:00 and cannot cross midnight.
/// This is configuration, not history, so periods may be deleted.
/// </summary>
public sealed class CourtOperatingHour : Entity
{
    private CourtOperatingHour()
    {
    }

    public CourtOperatingHour(Guid courtId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt, DateTime utcNow)
    {
        EnsureValid(dayOfWeek, opensAt, closesAt);
        CourtId = courtId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid CourtId { get; private set; }

    public Court Court { get; private set; } = null!;

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool Overlaps(CourtOperatingHour other) => Overlaps(other.DayOfWeek, other.OpensAt, other.ClosesAt);

    /// <summary>
    /// Half-open intervals on the same day: touching periods do not overlap.
    /// </summary>
    public bool Overlaps(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt) =>
        DayOfWeek == dayOfWeek && OpensAt < closesAt && ClosesAt > opensAt;

    /// <summary>
    /// Whether <paramref name="from"/>-<paramref name="to"/> lies entirely inside the union of the given
    /// periods of one day. Touching periods count as continuous; a gap between periods breaks coverage.
    /// </summary>
    public static bool Covers(IEnumerable<CourtOperatingHour> periodsOfDay, TimeOnly from, TimeOnly to)
    {
        if (from >= to)
        {
            return false;
        }

        var reached = from;
        foreach (var period in periodsOfDay.OrderBy(p => p.OpensAt))
        {
            if (period.OpensAt <= reached && period.ClosesAt > reached)
            {
                reached = period.ClosesAt;
                if (reached >= to)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Replaces the period. Callers must check it does not overlap the court's other periods.
    /// Setting the current values is a no-op.
    /// </summary>
    public void Reschedule(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt, DateTime utcNow)
    {
        EnsureValid(dayOfWeek, opensAt, closesAt);
        Guard.Utc(utcNow, nameof(utcNow));
        if (dayOfWeek == DayOfWeek && opensAt == OpensAt && closesAt == ClosesAt)
        {
            return;
        }

        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        UpdatedAtUtc = utcNow;
    }

    private static void EnsureValid(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (!Enum.IsDefined(dayOfWeek))
        {
            throw new DomainException("Invalid day of week.");
        }

        if (opensAt >= closesAt)
        {
            throw new DomainException("Opening time must be earlier than closing time.");
        }
    }
}
