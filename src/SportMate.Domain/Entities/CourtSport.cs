using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

/// <summary>
/// A sport offered on a physical court, with its own hourly price.
/// </summary>
public sealed class CourtSport : Entity
{
    private CourtSport()
    {
    }

    public CourtSport(Guid courtId, Guid sportId, decimal pricePerHour, string currency, DateTime utcNow)
    {
        CourtId = courtId;
        SportId = sportId;
        PricePerHour = Guard.Money(pricePerHour, nameof(pricePerHour), allowZero: false);
        Currency = Guard.Currency(currency);
        IsActive = true;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid CourtId { get; private set; }

    public Court Court { get; private set; } = null!;

    public Guid SportId { get; private set; }

    public Sport Sport { get; private set; } = null!;

    public decimal PricePerHour { get; private set; }

    public string Currency { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangePrice(decimal pricePerHour, string currency, DateTime utcNow)
    {
        PricePerHour = Guard.Money(pricePerHour, nameof(pricePerHour), allowZero: false);
        Currency = Guard.Currency(currency);
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void SetActive(bool isActive, DateTime utcNow)
    {
        IsActive = isActive;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }
}
