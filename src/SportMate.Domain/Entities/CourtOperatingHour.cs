using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

/// <summary>
/// One opening period of a court on a day of the week, in the court's local time
/// (<see cref="Court.TimeZoneId"/>). A day may have several non-overlapping periods.
/// </summary>
public sealed class CourtOperatingHour : Entity
{
    private CourtOperatingHour()
    {
    }

    public CourtOperatingHour(Guid courtId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (!Enum.IsDefined(dayOfWeek))
        {
            throw new DomainException("Invalid day of week.");
        }

        if (opensAt >= closesAt)
        {
            throw new DomainException("Opening time must be earlier than closing time.");
        }

        CourtId = courtId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Guid CourtId { get; private set; }

    public Court Court { get; private set; } = null!;

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    public bool Overlaps(CourtOperatingHour other) =>
        DayOfWeek == other.DayOfWeek && OpensAt < other.ClosesAt && ClosesAt > other.OpensAt;
}
