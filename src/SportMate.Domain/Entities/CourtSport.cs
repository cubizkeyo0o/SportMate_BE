using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

/// <summary>
/// A sport offered on a physical court, with its own hourly price. Never hard-deleted: removing a sport
/// from a court deactivates the relationship so bookings keep referring to it.
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
        PricePerHour = Guard.Money(pricePerHour, nameof(pricePerHour), allowZero: true);
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

    /// <summary>
    /// The price is per hour and may be zero. Setting the current price is a no-op.
    /// </summary>
    public void ChangePrice(decimal pricePerHour, string currency, DateTime utcNow)
    {
        pricePerHour = Guard.Money(pricePerHour, nameof(pricePerHour), allowZero: true);
        currency = Guard.Currency(currency);
        Guard.Utc(utcNow, nameof(utcNow));
        if (pricePerHour == PricePerHour && currency == Currency)
        {
            return;
        }

        PricePerHour = pricePerHour;
        Currency = currency;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Setting the current state is a no-op. Callers must not reactivate a relationship whose sport is inactive.
    /// </summary>
    public void SetActive(bool isActive, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (isActive == IsActive)
        {
            return;
        }

        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }
}
