using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

/// <summary>
/// A user's request to join a play request. Created and changed only through <see cref="PlayRequest"/>.
/// </summary>
public sealed class PlayRequestParticipant : Entity
{
    private PlayRequestParticipant()
    {
    }

    internal PlayRequestParticipant(Guid playRequestId, Guid userId, DateTime utcNow)
    {
        PlayRequestId = playRequestId;
        UserId = userId;
        Status = ParticipantStatus.Pending;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid PlayRequestId { get; private set; }

    public PlayRequest PlayRequest { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public ParticipantStatus Status { get; private set; }

    public DateTime? RespondedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    internal void Accept(DateTime utcNow) => Respond(ParticipantStatus.Accepted, utcNow);

    internal void Reject(DateTime utcNow) => Respond(ParticipantStatus.Rejected, utcNow);

    internal void Withdraw(DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (Status is not (ParticipantStatus.Pending or ParticipantStatus.Accepted))
        {
            throw new DomainException("Only pending or accepted participants can withdraw.");
        }

        Status = ParticipantStatus.Withdrawn;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    private void Respond(ParticipantStatus status, DateTime utcNow)
    {
        if (Status != ParticipantStatus.Pending)
        {
            throw new DomainException("Only pending participants can be accepted or rejected.");
        }

        Status = status;
        RespondedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    internal void Resubmit(DateTime utcNow)
    {
        if (Status is not (ParticipantStatus.Rejected or ParticipantStatus.Withdrawn))
        {
            throw new DomainException("Only rejected or withdrawn participants can request to join again.");
        }

        Status = ParticipantStatus.Pending;
        RespondedAtUtc = null;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }
}
