using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.UnitTests.Domain;

public sealed class PlayRequestTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static PlayRequest CreateRequest(Guid creatorId, byte spotsNeeded) =>
        new(creatorId, Guid.NewGuid(), "Doubles tonight", Start, Start.AddHours(2), spotsNeeded, 10.77m, 106.70m, Now);

    [Fact]
    public void Constructor_RejectsZeroSpotsNeeded()
    {
        Assert.Throws<DomainException>(() => CreateRequest(Guid.NewGuid(), 0));
    }

    [Fact]
    public void RequestToJoin_RejectsCreator()
    {
        var creatorId = Guid.NewGuid();
        var request = CreateRequest(creatorId, 3);

        Assert.Throws<DomainException>(() => request.RequestToJoin(creatorId, Now));
        Assert.Empty(request.Participants);
    }

    [Fact]
    public void RequestToJoin_RejectsDuplicateUser()
    {
        var request = CreateRequest(Guid.NewGuid(), 3);
        var userId = Guid.NewGuid();
        request.RequestToJoin(userId, Now);

        Assert.Throws<DomainException>(() => request.RequestToJoin(userId, Now));
    }

    [Fact]
    public void Accept_CannotExceedSpotsNeeded()
    {
        var request = CreateRequest(Guid.NewGuid(), 1);
        var first = request.RequestToJoin(Guid.NewGuid(), Now);
        var second = request.RequestToJoin(Guid.NewGuid(), Now);

        request.Accept(first.Id, Now);

        Assert.Equal(PlayRequestStatus.Full, request.Status);
        Assert.Throws<DomainException>(() => request.Accept(second.Id, Now));
        Assert.Equal(1, request.AcceptedCount);
    }

    [Fact]
    public void Withdraw_FromFullRequest_ReopensIt()
    {
        var request = CreateRequest(Guid.NewGuid(), 1);
        var participant = request.RequestToJoin(Guid.NewGuid(), Now);
        request.Accept(participant.Id, Now);

        request.Withdraw(participant.UserId, Now);

        Assert.Equal(ParticipantStatus.Withdrawn, participant.Status);
        Assert.Equal(PlayRequestStatus.Open, request.Status);
    }

    [Fact]
    public void SetSkillRange_RejectsMinAboveMax()
    {
        var request = CreateRequest(Guid.NewGuid(), 2);

        Assert.Throws<DomainException>(() =>
            request.SetSkillRange(SkillLevel.Advanced, SkillLevel.Beginner, Now));
    }
}
