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
    public const int CityMaxLength = 100;
    public const int TimeZoneIdMaxLength = 64;

    private Court()
    {
    }

    public Court(
        Guid ownerId,
        string name,
        string? description,
        string address,
        string city,
        decimal latitude,
        decimal longitude,
        string timeZoneId,
        DateTime utcNow)
    {
        Guard.Coordinates(latitude, longitude);
        OwnerId = ownerId == Guid.Empty ? throw new DomainException("Owner is required.") : ownerId;
        Name = Guard.NotBlank(name, NameMaxLength, nameof(name));
        Description = Guard.Optional(description, DescriptionMaxLength, nameof(description));
        Address = Guard.NotBlank(address, AddressMaxLength, nameof(address));
        City = Guard.NotBlank(city, CityMaxLength, nameof(city));
        Latitude = latitude;
        Longitude = longitude;
        TimeZoneId = Guard.NotBlank(timeZoneId, TimeZoneIdMaxLength, nameof(timeZoneId));
        Status = CourtStatus.Active;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// The user with the CourtOwner role who manages this court.
    /// </summary>
    public Guid OwnerId { get; private set; }

    public User Owner { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Address { get; private set; } = null!;

    public string City { get; private set; } = null!;

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

    /// <summary>
    /// Replaces the descriptive and location fields. <see cref="UpdatedAtUtc"/> changes only if a value did.
    /// </summary>
    public void UpdateDetails(
        string name,
        string? description,
        string address,
        string city,
        decimal latitude,
        decimal longitude,
        string timeZoneId,
        DateTime utcNow)
    {
        Guard.Coordinates(latitude, longitude);
        name = Guard.NotBlank(name, NameMaxLength, nameof(name));
        description = Guard.Optional(description, DescriptionMaxLength, nameof(description));
        address = Guard.NotBlank(address, AddressMaxLength, nameof(address));
        city = Guard.NotBlank(city, CityMaxLength, nameof(city));
        timeZoneId = Guard.NotBlank(timeZoneId, TimeZoneIdMaxLength, nameof(timeZoneId));
        Guard.Utc(utcNow, nameof(utcNow));

        if (name == Name && description == Description && address == Address && city == City
            && latitude == Latitude && longitude == Longitude && timeZoneId == TimeZoneId)
        {
            return;
        }

        Name = name;
        Description = description;
        Address = address;
        City = city;
        Latitude = latitude;
        Longitude = longitude;
        TimeZoneId = timeZoneId;
        UpdatedAtUtc = utcNow;
    }

    public void SetDescription(string? description, DateTime utcNow)
    {
        Description = Guard.Optional(description, DescriptionMaxLength, nameof(description));
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    /// <summary>
    /// Active and Inactive may be switched either way; setting the current status is a no-op.
    /// </summary>
    public void SetStatus(CourtStatus status, DateTime utcNow)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainException("Invalid court status.");
        }

        Guard.Utc(utcNow, nameof(utcNow));
        if (status == Status)
        {
            return;
        }

        Status = status;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Hands the court to another owner. Callers must verify the new owner is an active CourtOwner.
    /// </summary>
    public void AssignOwner(Guid ownerId, DateTime utcNow)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Owner is required.");
        }

        Guard.Utc(utcNow, nameof(utcNow));
        if (ownerId == OwnerId)
        {
            return;
        }

        OwnerId = ownerId;
        UpdatedAtUtc = utcNow;
    }
}
