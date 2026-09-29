using SportMate.Domain.Common;
using SportMate.Domain.Entities;

namespace SportMate.UnitTests.Domain;

public sealed class SportTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(5);

    [Theory]
    [InlineData("tennis")]
    [InlineData("table-tennis")]
    [InlineData("3x3-basketball")]
    public void Constructor_AcceptsLowercaseHyphenSeparatedSlugs(string slug)
    {
        var sport = new Sport("Some Sport", slug, Now);

        Assert.Equal(slug, sport.Slug);
        Assert.True(sport.IsActive);
        Assert.Equal(Now, sport.CreatedAtUtc);
        Assert.Equal(Now, sport.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tennis")]
    [InlineData("table tennis")]
    [InlineData("table_tennis")]
    [InlineData("-tennis")]
    [InlineData("tennis-")]
    [InlineData("table--tennis")]
    public void Constructor_RejectsInvalidSlugs(string slug)
    {
        Assert.Throws<DomainException>(() => new Sport("Some Sport", slug, Now));
    }

    [Fact]
    public void Constructor_RejectsSlugLongerThanColumn()
    {
        Assert.Throws<DomainException>(() => new Sport("Some Sport", new string('a', Sport.SlugMaxLength + 1), Now));
    }

    [Fact]
    public void SetActive_TogglesAndTouchesUpdatedAtOnlyOnChange()
    {
        var sport = new Sport("Tennis", "tennis", Now);

        sport.SetActive(true, Later);
        Assert.Equal(Now, sport.UpdatedAtUtc);

        sport.SetActive(false, Later);
        Assert.False(sport.IsActive);
        Assert.Equal(Later, sport.UpdatedAtUtc);
    }
}
