using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

/// <summary>
/// A user looking for additional players. Never hard-deleted.
/// </summary>
public sealed class PlayRequest : Entity
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int LocationNameMaxLength = 200;

    private PlayRequest()
    {
    }

    public PlayRequest(
        Guid creatorId,
        Guid sportId,
        string title,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        byte spotsNeeded,
        decimal latitude,
        decimal longitude,
        DateTime utcNow)
    {
        Guard.UtcRange(startsAtUtc, endsAtUtc);
        Guard.Coordinates(latitude, longitude);
        if (spotsNeeded == 0)
        {
            throw new DomainException("Spots needed must be positive.");
        }

        CreatorId = creatorId;
        SportId = sportId;
        Title = Guard.NotBlank(title, TitleMaxLength, nameof(title));
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        SpotsNeeded = spotsNeeded;
        Latitude = latitude;
        Longitude = longitude;
        Status = PlayRequestStatus.Open;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public Guid CreatorId { get; private set; }

    public User Creator { get; private set; } = null!;

    public Guid SportId { get; private set; }

    public Sport Sport { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateTime StartsAtUtc { get; private set; }

    public DateTime EndsAtUtc { get; private set; }

    /// <summary>
    /// Number of additional players needed, not counting the creator.
    /// </summary>
    public byte SpotsNeeded { get; private set; }

    public SkillLevel? MinSkillLevel { get; private set; }

    public SkillLevel? MaxSkillLevel { get; private set; }

    public string? LocationName { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public PlayRequestStatus Status { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<PlayRequestParticipant> Participants { get; private set; } = new List<PlayRequestParticipant>();

    public int AcceptedCount => Participants.Count(p => p.Status == ParticipantStatus.Accepted);

    public void SetDetails(string? description, string? locationName, DateTime utcNow)
    {
        Description = Guard.Optional(description, DescriptionMaxLength, nameof(description));
        LocationName = Guard.Optional(locationName, LocationNameMaxLength, nameof(locationName));
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void SetSkillRange(SkillLevel? min, SkillLevel? max, DateTime utcNow)
    {
        if ((min is { } a && !Enum.IsDefined(a)) || (max is { } b && !Enum.IsDefined(b)))
        {
            throw new DomainException("Invalid skill level.");
        }

        if (min > max)
        {
            throw new DomainException("Minimum skill level cannot exceed maximum skill level.");
        }

        MinSkillLevel = min;
        MaxSkillLevel = max;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    /// <summary>
    /// Adds a pending join request. Requires <see cref="Participants"/> to be loaded.
    /// </summary>
    public PlayRequestParticipant RequestToJoin(Guid userId, DateTime utcNow)
    {
        if (userId == CreatorId)
        {
            throw new DomainException("The creator cannot join their own play request.");
        }

        if (Status != PlayRequestStatus.Open)
        {
            throw new DomainException("The play request is not open.");
        }

        if (Participants.Any(p => p.UserId == userId))
        {
            throw new DomainException("The user has already requested to join.");
        }

        var participant = new PlayRequestParticipant(Id, userId, utcNow);
        Participants.Add(participant);
        UpdatedAtUtc = utcNow;
        return participant;
    }

    /// <summary>
    /// Accepts a pending participant. Requires <see cref="Participants"/> to be loaded; callers must
    /// lock the play request row so concurrent accepts cannot exceed <see cref="SpotsNeeded"/>.
    /// </summary>
    public void Accept(Guid participantId, DateTime utcNow)
    {
        var participant = FindParticipant(participantId);
        if (Status != PlayRequestStatus.Open)
        {
            throw new DomainException("The play request is not open.");
        }

        if (AcceptedCount >= SpotsNeeded)
        {
            throw new DomainException("The play request has no remaining spots.");
        }

        participant.Accept(utcNow);
        if (AcceptedCount >= SpotsNeeded)
        {
            Status = PlayRequestStatus.Full;
        }

        UpdatedAtUtc = utcNow;
    }

    public void Reject(Guid participantId, DateTime utcNow)
    {
        FindParticipant(participantId).Reject(utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void Withdraw(Guid userId, DateTime utcNow)
    {
        var participant = Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new DomainException("The user is not a participant.");
        participant.Withdraw(utcNow);
        if (Status == PlayRequestStatus.Full && AcceptedCount < SpotsNeeded)
        {
            Status = PlayRequestStatus.Open;
        }

        UpdatedAtUtc = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new DomainException("Only open or full play requests can be cancelled.");
        }

        Status = PlayRequestStatus.Cancelled;
        CancelledAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    private PlayRequestParticipant FindParticipant(Guid participantId) =>
        Participants.FirstOrDefault(p => p.Id == participantId)
        ?? throw new DomainException("Participant not found.");
}
