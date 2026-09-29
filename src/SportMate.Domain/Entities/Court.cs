using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

/// <summary>
/// One physical court. Several sports may be played on it (see <see cref="CourtSport"/>),
/// and bookings for any of them conflict with each other.
/// </summary>
public sealed class Court : Entity
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int AddressMaxLength = 300;
    public const int TimeZoneIdMaxLength = 64;

    private Court()
    {
    }

    public Court(
        Guid ownerId,
        string name,
        string address,
        decimal latitude,
        decimal longitude,
        string timeZoneId,
        DateTime utcNow)
    {
        Guard.Coordinates(latitude, longitude);
        OwnerId = ownerId;
        Name = Guard.NotBlank(name, NameMaxLength, nameof(name));
        Address = Guard.NotBlank(address, AddressMaxLength, nameof(address));
        Latitude = latitude;
        Longitude = longitude;
        TimeZoneId = Guard.NotBlank(timeZoneId, TimeZoneIdMaxLength, nameof(timeZoneId));
        Status = CourtStatus.Active;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid OwnerId { get; private set; }

    public User Owner { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Address { get; private set; } = null!;

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    /// <summary>
    /// IANA time zone identifier, e.g. "Asia/Ho_Chi_Minh".
    /// </summary>
    public string TimeZoneId { get; private set; } = null!;

    public CourtStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<CourtSport> CourtSports { get; private set; } = new List<CourtSport>();

    public ICollection<CourtOperatingHour> OperatingHours { get; private set; } = new List<CourtOperatingHour>();

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    public void SetDescription(string? description, DateTime utcNow)
    {
        Description = Guard.Optional(description, DescriptionMaxLength, nameof(description));
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void SetStatus(CourtStatus status, DateTime utcNow)
    {
        Status = status;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }
}
