using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Domain;

public sealed class CourtTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(5);

    private static Court CreateCourt(Guid? ownerId = null) =>
        new(ownerId ?? Guid.NewGuid(), " Court A ", " Near the gate ", " 1 Tran Phu ", " Ha Noi ",
            21.028511m, 105.804817m, "Asia/Ho_Chi_Minh", Now);

    [Fact]
    public void Constructor_StartsActiveWithTrimmedFieldsAndEqualTimestamps()
    {
        var ownerId = Guid.NewGuid();

        var court = CreateCourt(ownerId);

        Assert.Equal(ownerId, court.OwnerId);
        Assert.Equal("Court A", court.Name);
        Assert.Equal("Near the gate", court.Description);
        Assert.Equal("1 Tran Phu", court.Address);
        Assert.Equal("Ha Noi", court.City);
        Assert.Equal(CourtStatus.Active, court.Status);
        Assert.Equal(Now, court.CreatedAtUtc);
        Assert.Equal(Now, court.UpdatedAtUtc);
    }

    [Fact]
    public void Constructor_RejectsEmptyOwner()
    {
        Assert.Throws<DomainException>(() => CreateCourt(Guid.Empty));
    }

    [Fact]
    public void Constructor_RejectsBlankCity()
    {
        Assert.Throws<DomainException>(() =>
            new Court(Guid.NewGuid(), "Court", null, "Address", " ", 0m, 0m, "Asia/Ho_Chi_Minh", Now));
    }

    [Theory]
    [InlineData(CourtStatus.Active, CourtStatus.Inactive)]
    [InlineData(CourtStatus.Inactive, CourtStatus.Active)]
    public void SetStatus_SwitchesBetweenActiveAndInactive(CourtStatus from, CourtStatus to)
    {
        var court = CreateCourt();
        court.SetStatus(from, Now);

        court.SetStatus(to, Later);

        Assert.Equal(to, court.Status);
        Assert.Equal(Later, court.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(CourtStatus.Active)]
    [InlineData(CourtStatus.Inactive)]
    public void SetStatus_ToCurrentStatus_IsANoOp(CourtStatus status)
    {
        var court = CreateCourt();
        court.SetStatus(status, Now);

        court.SetStatus(status, Later);

        Assert.Equal(status, court.Status);
        Assert.Equal(Now, court.UpdatedAtUtc);
    }

    [Fact]
    public void SetStatus_RejectsUndefinedStatus()
    {
        var court = CreateCourt();

        Assert.Throws<DomainException>(() => court.SetStatus((CourtStatus)99, Later));
    }

    [Fact]
    public void UpdateDetails_ReplacesFieldsAndTouchesUpdatedAt()
    {
        var court = CreateCourt();

        court.UpdateDetails("Court B", null, "2 Le Loi", "Da Nang", -10.5m, -20.25m, "Asia/Bangkok", Later);

        Assert.Equal("Court B", court.Name);
        Assert.Null(court.Description);
        Assert.Equal("2 Le Loi", court.Address);
        Assert.Equal("Da Nang", court.City);
        Assert.Equal(-10.5m, court.Latitude);
        Assert.Equal(-20.25m, court.Longitude);
        Assert.Equal("Asia/Bangkok", court.TimeZoneId);
        Assert.Equal(Now, court.CreatedAtUtc);
        Assert.Equal(Later, court.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_WithSameValues_KeepsUpdatedAt()
    {
        var court = CreateCourt();

        court.UpdateDetails("Court A", "Near the gate", "1 Tran Phu", "Ha Noi",
            21.028511m, 105.804817m, "Asia/Ho_Chi_Minh", Later);

        Assert.Equal(Now, court.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    public void UpdateDetails_RejectsOutOfRangeCoordinates(double latitude, double longitude)
    {
        var court = CreateCourt();

        Assert.Throws<DomainException>(() => court.UpdateDetails(
            "Court", null, "Address", "City", (decimal)latitude, (decimal)longitude, "Asia/Bangkok", Later));
    }

    [Fact]
    public void AssignOwner_ChangesOwnerAndTouchesUpdatedAt()
    {
        var court = CreateCourt();
        var newOwner = Guid.NewGuid();

        court.AssignOwner(newOwner, Later);

        Assert.Equal(newOwner, court.OwnerId);
        Assert.Equal(Later, court.UpdatedAtUtc);
    }

    [Fact]
    public void AssignOwner_RejectsEmptyOwner()
    {
        var court = CreateCourt();

        Assert.Throws<DomainException>(() => court.AssignOwner(Guid.Empty, Later));
    }
}
