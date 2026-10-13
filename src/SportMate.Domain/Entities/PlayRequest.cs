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

    public void UpdateMatchingDetails(
        Guid sportId,
        string title,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        byte spotsNeeded,
        decimal latitude,
        decimal longitude,
        SkillLevel? minSkillLevel,
        SkillLevel? maxSkillLevel,
        DateTime utcNow)
    {
        var normalizedTitle = Guard.NotBlank(title, TitleMaxLength, nameof(title));
        Guard.UtcRange(startsAtUtc, endsAtUtc);
        Guard.Coordinates(latitude, longitude);
        Guard.Utc(utcNow, nameof(utcNow));
        ValidateSpotsNeeded(spotsNeeded);
        ValidateSkillRange(minSkillLevel, maxSkillLevel);

        if (Participants.Count > 0
            && (SportId != sportId
                || Title != normalizedTitle
                || StartsAtUtc != startsAtUtc
                || EndsAtUtc != endsAtUtc
                || SpotsNeeded != spotsNeeded
                || Latitude != latitude
                || Longitude != longitude
                || MinSkillLevel != minSkillLevel
                || MaxSkillLevel != maxSkillLevel))
        {
            throw new DomainException("Matching details cannot be changed after a participant has requested to join.");
        }

        SportId = sportId;
        Title = normalizedTitle;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        SpotsNeeded = spotsNeeded;
        Latitude = latitude;
        Longitude = longitude;
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
        UpdatedAtUtc = utcNow;
    }

    public void SetSkillRange(SkillLevel? min, SkillLevel? max, DateTime utcNow)
    {
        ValidateSkillRange(min, max);
        if (Participants.Count > 0 && (MinSkillLevel != min || MaxSkillLevel != max))
        {
            throw new DomainException("Skill requirements cannot be changed after a participant has requested to join.");
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
            var existing = Participants.First(p => p.UserId == userId);
            existing.Resubmit(utcNow);
            UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
            return existing;
        }

        var participant = new PlayRequestParticipant(Id, userId, utcNow);
        Participants.Add(participant);
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        return participant;
    }

    /// <summary>
    /// Accepts a pending participant. Requires <see cref="Participants"/> to be loaded; callers must
    /// lock the play request row so concurrent accepts cannot exceed <see cref="SpotsNeeded"/>.
    /// </summary>
    public void Accept(Guid participantId, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
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
        Guard.Utc(utcNow, nameof(utcNow));
        EnsureCanManageParticipants();
        FindParticipant(participantId).Reject(utcNow);
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void Withdraw(Guid userId, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        EnsureCanManageParticipants();
        var participant = Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new DomainException("The user is not a participant.");
        participant.Withdraw(utcNow);
        if (Status == PlayRequestStatus.Full && AcceptedCount < SpotsNeeded)
        {
            Status = PlayRequestStatus.Open;
        }

        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void Cancel(DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new DomainException("Only open or full play requests can be cancelled.");
        }

        Status = PlayRequestStatus.Cancelled;
        CancelledAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public void Complete(DateTime utcNow)
    {
        if (Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new DomainException("Only open or full play requests can be completed.");
        }

        Status = PlayRequestStatus.Completed;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    private void EnsureCanManageParticipants()
    {
        if (Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new DomainException("Participants cannot be changed on a cancelled or completed play request.");
        }
    }

    private static void ValidateSpotsNeeded(byte spotsNeeded)
    {
        if (spotsNeeded == 0)
        {
            throw new DomainException("Spots needed must be positive.");
        }
    }

    private static void ValidateSkillRange(SkillLevel? min, SkillLevel? max)
    {
        if ((min is { } a && !Enum.IsDefined(a)) || (max is { } b && !Enum.IsDefined(b)))
        {
            throw new DomainException("Invalid skill level.");
        }

        if (min > max)
        {
            throw new DomainException("Minimum skill level cannot exceed maximum skill level.");
        }
    }

    private PlayRequestParticipant FindParticipant(Guid participantId) =>
        Participants.FirstOrDefault(p => p.Id == participantId)
        ?? throw new DomainException("Participant not found.");
}
