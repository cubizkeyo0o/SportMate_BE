using SportMate.Domain.Common;
using SportMate.Domain.Entities;

namespace SportMate.UnitTests.Domain;

public sealed class CourtSportTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(5);

    private static CourtSport Create(decimal price = 100_000m) => new(Guid.NewGuid(), Guid.NewGuid(), price, "vnd", Now);

    [Fact]
    public void Constructor_StartsActiveWithUppercaseCurrency()
    {
        var courtSport = Create();

        Assert.True(courtSport.IsActive);
        Assert.Equal("VND", courtSport.Currency);
        Assert.Equal(Now, courtSport.CreatedAtUtc);
        Assert.Equal(Now, courtSport.UpdatedAtUtc);
    }

    [Fact]
    public void Constructor_AcceptsZeroPrice()
    {
        Assert.Equal(0m, Create(0m).PricePerHour);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.005)]
    [InlineData(100000000)]
    public void Constructor_RejectsInvalidPrices(double price)
    {
        Assert.Throws<DomainException>(() => Create((decimal)price));
    }

    [Theory]
    [InlineData("VN")]
    [InlineData("VNDD")]
    [InlineData("V1D")]
    public void Constructor_RejectsInvalidCurrency(string currency)
    {
        Assert.Throws<DomainException>(() => new CourtSport(Guid.NewGuid(), Guid.NewGuid(), 1m, currency, Now));
    }

    [Fact]
    public void ChangePrice_UpdatesAndTouchesTimestamp_OrIsNoOpWhenUnchanged()
    {
        var courtSport = Create();

        courtSport.ChangePrice(100_000m, "VND", Later);
        Assert.Equal(Now, courtSport.UpdatedAtUtc);

        courtSport.ChangePrice(0m, "usd", Later);
        Assert.Equal(0m, courtSport.PricePerHour);
        Assert.Equal("USD", courtSport.Currency);
        Assert.Equal(Later, courtSport.UpdatedAtUtc);
    }

    [Fact]
    public void SetActive_TogglesAndIsNoOpWhenUnchanged()
    {
        var courtSport = Create();

        courtSport.SetActive(true, Later);
        Assert.Equal(Now, courtSport.UpdatedAtUtc);

        courtSport.SetActive(false, Later);
        Assert.False(courtSport.IsActive);
        Assert.Equal(Later, courtSport.UpdatedAtUtc);
    }
}
