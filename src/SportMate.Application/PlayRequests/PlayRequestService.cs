using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportMate.Application.Common;
using SportMate.Domain.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.PlayRequests;

public sealed class PlayRequestService(IApplicationDbContext db, TimeProvider timeProvider) : IPlayRequestService
{
    private const int MaximumPageSize = 50;
    private const double MaximumRadiusKm = 100;
    private const double EarthRadiusKm = 6371.0088;

    public async Task<PlayRequestPageResponse> DiscoverAsync(
        Guid userId,
        PlayRequestQuery query,
        CancellationToken cancellationToken)
    {
        var page = ValidatePage(query.Page, query.PageSize);
        ValidateDiscoveryFilters(query);

        var matchingProfile = db.UserSportProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId);

        // Match the viewer's current profile and the host's inclusive optional skill bounds.
        var requests = db.PlayRequests.AsNoTracking()
            .Include(playRequest => playRequest.Sport)
            .Include(playRequest => playRequest.Creator)
            .Where(playRequest =>
                (playRequest.Status == PlayRequestStatus.Open || playRequest.Status == PlayRequestStatus.Full)
                && playRequest.Sport.IsActive
                && matchingProfile.Any(profile =>
                    profile.SportId == playRequest.SportId
                    && (!playRequest.MinSkillLevel.HasValue || profile.SkillLevel >= playRequest.MinSkillLevel.Value)
                    && (!playRequest.MaxSkillLevel.HasValue || profile.SkillLevel <= playRequest.MaxSkillLevel.Value)));

        if (query.SportId is { } sportId)
        {
            requests = requests.Where(playRequest => playRequest.SportId == sportId);
        }

        if (query.StartsFromUtc is { } from)
        {
            var fromUtc = from.UtcDateTime;
            requests = requests.Where(playRequest => playRequest.StartsAtUtc >= fromUtc);
        }

        if (query.StartsToUtc is { } to)
        {
            var toUtc = to.UtcDateTime;
            requests = requests.Where(playRequest => playRequest.StartsAtUtc <= toUtc);
        }

        var useRadius = query.RadiusKm.HasValue;
        if (useRadius)
        {
            requests = ApplyRadiusBoundingBox(
                requests,
                (double)query.Latitude!.Value,
                (double)query.Longitude!.Value,
                query.RadiusKm!.Value);
        }

        var candidates = await requests
            .OrderBy(playRequest => playRequest.StartsAtUtc)
            .ThenBy(playRequest => playRequest.Id)
            .ToListAsync(cancellationToken);

        if (useRadius)
        {
            var centerLatitude = (double)query.Latitude!.Value;
            var centerLongitude = (double)query.Longitude!.Value;
            var radiusKm = query.RadiusKm!.Value;
            candidates = candidates
                .Where(playRequest => DistanceKm(
                    centerLatitude,
                    centerLongitude,
                    (double)playRequest.Latitude,
                    (double)playRequest.Longitude) <= radiusKm)
                .ToList();
        }

        var totalCount = candidates.Count;
        var items = candidates.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToList();
        return new PlayRequestPageResponse(
            await BuildResponsesAsync(items, userId, cancellationToken),
            page.Page,
            page.PageSize,
            totalCount);
    }

    public async Task<PlayRequestResponse> GetAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var request = await LoadRequestAsync(playRequestId, cancellationToken);
        if (request.CreatorId != userId && !await CanDiscoverAsync(request, userId, cancellationToken))
        {
            throw RequestNotFound();
        }

        return (await BuildResponsesAsync([request], userId, cancellationToken))[0];
    }

    public async Task<PlayRequestPageResponse> ListMineAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var paging = ValidatePage(page, pageSize);
        var query = db.PlayRequests.AsNoTracking()
            .Include(playRequest => playRequest.Sport)
            .Include(playRequest => playRequest.Creator)
            .Where(playRequest => playRequest.CreatorId == userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(playRequest => playRequest.CreatedAtUtc)
            .ThenBy(playRequest => playRequest.Id)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PlayRequestPageResponse(
            await BuildResponsesAsync(requests, userId, cancellationToken),
            paging.Page,
            paging.PageSize,
            totalCount);
    }

    public async Task<PlayRequestPageResponse> ListMyParticipationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var paging = ValidatePage(page, pageSize);
        var query = db.PlayRequests.AsNoTracking()
            .Include(playRequest => playRequest.Sport)
            .Include(playRequest => playRequest.Creator)
            .Where(playRequest => playRequest.Participants.Any(participant => participant.UserId == userId));
        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(playRequest => playRequest.CreatedAtUtc)
            .ThenBy(playRequest => playRequest.Id)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PlayRequestPageResponse(
            await BuildResponsesAsync(requests, userId, cancellationToken),
            paging.Page,
            paging.PageSize,
            totalCount);
    }

    public async Task<PlayRequestResponse> CreateAsync(
        Guid userId,
        CreatePlayRequestRequest request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var input = PlayRequestInputRules.Validate(
            request.SportId, request.Title, request.Description, request.StartsAt, request.EndsAt,
            request.SpotsNeeded, request.Latitude, request.Longitude, request.MinSkillLevel,
            request.MaxSkillLevel, request.LocationName, now);
        await EnsureActiveSportAsync(input.SportId, cancellationToken);
        var hostProfile = await db.UserSportProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId && profile.SportId == input.SportId, cancellationToken);
        if (hostProfile is null)
        {
            throw new ConflictException("Add a sport profile before creating a play request for this sport.");
        }

        var playRequest = new PlayRequest(
            userId, input.SportId, input.Title, input.StartsAtUtc, input.EndsAtUtc,
            input.SpotsNeeded, input.Latitude, input.Longitude, now);
        playRequest.SetDetails(input.Description, input.LocationName, now);
        playRequest.SetSkillRange(input.MinSkillLevel, input.MaxSkillLevel, now);
        db.PlayRequests.Add(playRequest);
        await db.SaveChangesAsync(cancellationToken);

        var created = await LoadRequestAsync(playRequest.Id, cancellationToken);
        return (await BuildResponsesAsync([created], userId, cancellationToken))[0];
    }

    public async Task<PlayRequestResponse> UpdateAsync(
        Guid playRequestId,
        Guid userId,
        UpdatePlayRequestRequest request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var input = PlayRequestInputRules.Validate(
            request.SportId, request.Title, request.Description, request.StartsAt, request.EndsAt,
            request.SpotsNeeded, request.Latitude, request.Longitude, request.MinSkillLevel,
            request.MaxSkillLevel, request.LocationName, now, requireFutureStart: false);

        var locked = await LockRequestAsync(playRequestId, cancellationToken);
        await using var transaction = locked.Transaction;
        var playRequest = locked.Request;
        EnsureHost(playRequest, userId);
        EnsureEditable(playRequest);

        var protectedDetailsChanged = MatchingDetailsChanged(playRequest, input);
        if (protectedDetailsChanged && input.StartsAtUtc <= now)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["starts_at"] = ["Start time must be in the future."]
            });
        }

        if (protectedDetailsChanged)
        {
            await EnsureActiveSportAsync(input.SportId, cancellationToken);
            var hostProfile = await db.UserSportProfiles.AsNoTracking()
                .AnyAsync(profile => profile.UserId == userId && profile.SportId == input.SportId, cancellationToken);
            if (!hostProfile)
            {
                throw new ConflictException("Add a sport profile before changing the request to this sport.");
            }
        }

        try
        {
            playRequest.UpdateMatchingDetails(
                input.SportId, input.Title, input.StartsAtUtc, input.EndsAtUtc, input.SpotsNeeded,
                input.Latitude, input.Longitude, input.MinSkillLevel, input.MaxSkillLevel, now);
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        playRequest.SetDetails(input.Description, input.LocationName, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildResponsesAsync([playRequest], userId, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<PlayRequestParticipantResponse>> ListParticipantsAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var request = await LoadRequestAsync(playRequestId, cancellationToken);
        EnsureHost(request, userId);
        var participants = await db.PlayRequestParticipants.AsNoTracking()
            .Include(participant => participant.User)
            .Where(participant => participant.PlayRequestId == playRequestId)
            .OrderByDescending(participant => participant.CreatedAtUtc)
            .ThenBy(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return await BuildParticipantResponsesAsync(participants, request.SportId, cancellationToken);
    }

    public async Task<PlayRequestResponse> JoinAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var locked = await LockRequestAsync(playRequestId, cancellationToken);
        await using var transaction = locked.Transaction;
        var playRequest = locked.Request;
        if (!await CanDiscoverAsync(playRequest, userId, cancellationToken))
        {
            throw RequestNotFound();
        }

        if (playRequest.Status != PlayRequestStatus.Open)
        {
            throw new ConflictException("This play request is not accepting join requests.");
        }

        try
        {
            playRequest.RequestToJoin(userId, UtcNow);
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildResponsesAsync([playRequest], userId, cancellationToken))[0];
    }

    public Task<PlayRequestResponse> AcceptAsync(
        Guid playRequestId,
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        RespondAsync(playRequestId, participantId, userId, accept: true, cancellationToken);

    public Task<PlayRequestResponse> RejectAsync(
        Guid playRequestId,
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        RespondAsync(playRequestId, participantId, userId, accept: false, cancellationToken);

    public async Task<PlayRequestResponse> WithdrawAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var locked = await LockRequestAsync(playRequestId, cancellationToken);
        await using var transaction = locked.Transaction;
        var playRequest = locked.Request;
        try
        {
            playRequest.Withdraw(userId, UtcNow);
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildResponsesAsync([playRequest], userId, cancellationToken))[0];
    }

    public Task<PlayRequestResponse> CancelAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken) =>
        ChangeRequestStatusAsync(playRequestId, userId, complete: false, cancellationToken);

    public Task<PlayRequestResponse> CompleteAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken) =>
        ChangeRequestStatusAsync(playRequestId, userId, complete: true, cancellationToken);

    private async Task<PlayRequestResponse> RespondAsync(
        Guid playRequestId,
        Guid participantId,
        Guid userId,
        bool accept,
        CancellationToken cancellationToken)
    {
        var locked = await LockRequestAsync(playRequestId, cancellationToken);
        await using var transaction = locked.Transaction;
        var playRequest = locked.Request;
        EnsureHost(playRequest, userId);
        var participant = playRequest.Participants.FirstOrDefault(item => item.Id == participantId)
            ?? throw RequestNotFound();
        if (participant.Status != ParticipantStatus.Pending)
        {
            throw new ConflictException("Only pending participants can be accepted or rejected.");
        }

        if (playRequest.Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new ConflictException("Participants cannot be changed on a cancelled or completed request.");
        }

        if (accept && playRequest.Status == PlayRequestStatus.Full)
        {
            throw new ConflictException("This play request has no remaining spots.");
        }

        try
        {
            if (accept)
            {
                playRequest.Accept(participantId, UtcNow);
            }
            else
            {
                playRequest.Reject(participantId, UtcNow);
            }
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildResponsesAsync([playRequest], userId, cancellationToken))[0];
    }

    private async Task<PlayRequestResponse> ChangeRequestStatusAsync(
        Guid playRequestId,
        Guid userId,
        bool complete,
        CancellationToken cancellationToken)
    {
        var locked = await LockRequestAsync(playRequestId, cancellationToken);
        await using var transaction = locked.Transaction;
        var playRequest = locked.Request;
        EnsureHost(playRequest, userId);

        try
        {
            if (complete)
            {
                playRequest.Complete(UtcNow);
            }
            else
            {
                playRequest.Cancel(UtcNow);
            }
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildResponsesAsync([playRequest], userId, cancellationToken))[0];
    }

    private async Task<(IDbContextTransaction Transaction, PlayRequest Request)> LockRequestAsync(
        Guid playRequestId,
        CancellationToken cancellationToken)
    {
        if (!await db.PlayRequests.AnyAsync(playRequest => playRequest.Id == playRequestId, cancellationToken))
        {
            throw RequestNotFound();
        }

        var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await db.LockPlayRequestForUpdateAsync(playRequestId, cancellationToken);
            var request = await db.PlayRequests
                .Include(playRequest => playRequest.Sport)
                .Include(playRequest => playRequest.Creator)
                .Include(playRequest => playRequest.Participants)
                .FirstOrDefaultAsync(playRequest => playRequest.Id == playRequestId, cancellationToken);
            if (request is null)
            {
                await transaction.DisposeAsync();
                throw RequestNotFound();
            }

            return (transaction, request);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task<PlayRequest> LoadRequestAsync(Guid playRequestId, CancellationToken cancellationToken) =>
        await db.PlayRequests.AsNoTracking()
            .Include(playRequest => playRequest.Sport)
            .Include(playRequest => playRequest.Creator)
            .FirstOrDefaultAsync(playRequest => playRequest.Id == playRequestId, cancellationToken)
        ?? throw RequestNotFound();

    private async Task<bool> CanDiscoverAsync(PlayRequest playRequest, Guid userId, CancellationToken cancellationToken)
    {
        if (playRequest.Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full) || !playRequest.Sport.IsActive)
        {
            return false;
        }

        return await db.UserSportProfiles.AsNoTracking().AnyAsync(profile =>
            profile.UserId == userId
            && profile.SportId == playRequest.SportId
            && (!playRequest.MinSkillLevel.HasValue || profile.SkillLevel >= playRequest.MinSkillLevel.Value)
            && (!playRequest.MaxSkillLevel.HasValue || profile.SkillLevel <= playRequest.MaxSkillLevel.Value),
            cancellationToken);
    }

    private async Task EnsureActiveSportAsync(Guid sportId, CancellationToken cancellationToken)
    {
        if (!await db.Sports.AsNoTracking().AnyAsync(sport => sport.Id == sportId && sport.IsActive, cancellationToken))
        {
            throw new NotFoundException("Active sport not found.");
        }
    }

    private static bool MatchingDetailsChanged(PlayRequest playRequest, PlayRequestInput input) =>
        playRequest.SportId != input.SportId
        || playRequest.Title != input.Title
        || playRequest.StartsAtUtc != input.StartsAtUtc
        || playRequest.EndsAtUtc != input.EndsAtUtc
        || playRequest.SpotsNeeded != input.SpotsNeeded
        || playRequest.Latitude != input.Latitude
        || playRequest.Longitude != input.Longitude
        || playRequest.MinSkillLevel != input.MinSkillLevel
        || playRequest.MaxSkillLevel != input.MaxSkillLevel;

    private static void EnsureEditable(PlayRequest playRequest)
    {
        if (playRequest.Status is not (PlayRequestStatus.Open or PlayRequestStatus.Full))
        {
            throw new ConflictException("Only open or full play requests can be edited.");
        }

    }

    private static void EnsureHost(PlayRequest playRequest, Guid userId)
    {
        if (playRequest.CreatorId != userId)
        {
            throw RequestNotFound();
        }
    }

    private async Task<IReadOnlyList<PlayRequestResponse>> BuildResponsesAsync(
        IReadOnlyList<PlayRequest> requests,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var requestIds = requests.Select(request => request.Id).ToList();
        var creatorIds = requests.Select(request => request.CreatorId).Distinct().ToList();
        var sportIds = requests.Select(request => request.SportId).Distinct().ToList();
        var skillProfiles = await db.UserSportProfiles.AsNoTracking()
            .Where(profile => creatorIds.Contains(profile.UserId) && sportIds.Contains(profile.SportId))
            .ToDictionaryAsync(profile => (profile.UserId, profile.SportId), profile => (SkillLevel?)profile.SkillLevel, cancellationToken);
        var participants = await db.PlayRequestParticipants.AsNoTracking()
            .Where(participant => requestIds.Contains(participant.PlayRequestId))
            .ToListAsync(cancellationToken);
        var acceptedCounts = participants
            .Where(participant => participant.Status == ParticipantStatus.Accepted)
            .GroupBy(participant => participant.PlayRequestId)
            .ToDictionary(group => group.Key, group => group.Count());
        var myStatuses = participants
            .Where(participant => participant.UserId == viewerId)
            .ToDictionary(participant => participant.PlayRequestId, participant => (ParticipantStatus?)participant.Status);
        var hostRequestIds = requests.Where(request => request.CreatorId == viewerId).Select(request => request.Id).ToList();
        var visibleParticipants = hostRequestIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<PlayRequestParticipantResponse>>()
            : await BuildHostParticipantGroupsAsync(hostRequestIds, requests, cancellationToken);

        return requests.Select(request => new PlayRequestResponse(
            request.Id,
            request.SportId,
            request.Sport.Name,
            request.Sport.Slug,
            request.Title,
            request.Description,
            AsUtcOffset(request.StartsAtUtc),
            AsUtcOffset(request.EndsAtUtc),
            request.SpotsNeeded,
            acceptedCounts.GetValueOrDefault(request.Id),
            request.LocationName,
            request.MinSkillLevel,
            request.MaxSkillLevel,
            request.Status,
            new PlayRequestHostResponse(
                request.Creator.DisplayName,
                skillProfiles.GetValueOrDefault((request.CreatorId, request.SportId))),
            myStatuses.GetValueOrDefault(request.Id),
            visibleParticipants.GetValueOrDefault(request.Id, []),
            request.CreatorId == viewerId ? request.Latitude : null,
            request.CreatorId == viewerId ? request.Longitude : null,
            AsUtcOffset(request.CreatedAtUtc),
            AsUtcOffset(request.UpdatedAtUtc))).ToList();
    }

    private async Task<Dictionary<Guid, IReadOnlyList<PlayRequestParticipantResponse>>> BuildHostParticipantGroupsAsync(
        List<Guid> hostRequestIds,
        IReadOnlyList<PlayRequest> requests,
        CancellationToken cancellationToken)
    {
        var participants = await db.PlayRequestParticipants.AsNoTracking()
            .Include(participant => participant.User)
            .Where(participant => hostRequestIds.Contains(participant.PlayRequestId))
            .OrderByDescending(participant => participant.CreatedAtUtc)
            .ThenBy(participant => participant.Id)
            .ToListAsync(cancellationToken);
        var sportByRequest = requests.ToDictionary(request => request.Id, request => request.SportId);
        var userIds = participants.Select(participant => participant.UserId).Distinct().ToList();
        var sportIds = sportByRequest.Values.Distinct().ToList();
        var profiles = await db.UserSportProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId) && sportIds.Contains(profile.SportId))
            .ToDictionaryAsync(profile => (profile.UserId, profile.SportId), profile => (SkillLevel?)profile.SkillLevel, cancellationToken);

        return participants
            .GroupBy(participant => participant.PlayRequestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PlayRequestParticipantResponse>)group.Select(participant =>
                    new PlayRequestParticipantResponse(
                        participant.Id,
                        participant.User.DisplayName,
                        profiles.GetValueOrDefault((participant.UserId, sportByRequest[participant.PlayRequestId])),
                        participant.Status,
                        AsUtcOffset(participant.CreatedAtUtc))).ToList());
    }

    private async Task<IReadOnlyList<PlayRequestParticipantResponse>> BuildParticipantResponsesAsync(
        IReadOnlyList<PlayRequestParticipant> participants,
        Guid sportId,
        CancellationToken cancellationToken)
    {
        var userIds = participants.Select(participant => participant.UserId).Distinct().ToList();
        var profiles = await db.UserSportProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId) && profile.SportId == sportId)
            .ToDictionaryAsync(profile => profile.UserId, profile => (SkillLevel?)profile.SkillLevel, cancellationToken);
        return participants.Select(participant => new PlayRequestParticipantResponse(
            participant.Id,
            participant.User.DisplayName,
            profiles.GetValueOrDefault(participant.UserId),
            participant.Status,
            AsUtcOffset(participant.CreatedAtUtc))).ToList();
    }

    private static IQueryable<PlayRequest> ApplyRadiusBoundingBox(
        IQueryable<PlayRequest> requests,
        double latitude,
        double longitude,
        double radiusKm)
    {
        var lat = latitude;
        var lon = longitude;
        var latDelta = radiusKm / 110.574;
        var minLat = Math.Max(-90, lat - latDelta);
        var maxLat = Math.Min(90, lat + latDelta);
        requests = requests.Where(request => request.Latitude >= (decimal)minLat && request.Latitude <= (decimal)maxLat);

        var longitudeScale = Math.Cos(DegreesToRadians(lat));
        if (Math.Abs(longitudeScale) < 0.000001)
        {
            return requests;
        }

        var lonDelta = radiusKm / (111.320 * Math.Abs(longitudeScale));
        if (lonDelta >= 180)
        {
            return requests;
        }

        var minLon = lon - lonDelta;
        var maxLon = lon + lonDelta;
        if (minLon < -180)
        {
            return requests.Where(request => request.Longitude >= (decimal)(minLon + 360)
                || request.Longitude <= (decimal)maxLon);
        }

        if (maxLon > 180)
        {
            return requests.Where(request => request.Longitude >= (decimal)minLon
                || request.Longitude <= (decimal)(maxLon - 360));
        }

        return requests.Where(request => request.Longitude >= (decimal)minLon && request.Longitude <= (decimal)maxLon);
    }

    private static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(DegreesToRadians(latitude1))
            * Math.Cos(DegreesToRadians(latitude2))
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180);

    private static (int Page, int PageSize) ValidatePage(int page, int pageSize)
    {
        var errors = new ValidationErrors();
        if (page < 1)
        {
            errors.Add("page", "Page must be at least 1.");
        }

        if (pageSize < 1 || pageSize > MaximumPageSize)
        {
            errors.Add("page_size", $"Page size must be between 1 and {MaximumPageSize}.");
        }

        errors.ThrowIfAny();
        return (page, pageSize);
    }

    private static void ValidateDiscoveryFilters(PlayRequestQuery query)
    {
        var errors = new ValidationErrors();
        if (query.StartsFromUtc is { } from
            && query.StartsToUtc is { } to
            && from > to)
        {
            errors.Add("starts_to_utc", "End of the start-time range must not be earlier than its beginning.");
        }

        var anyLocation = query.Latitude.HasValue || query.Longitude.HasValue || query.RadiusKm.HasValue;
        var allLocation = query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusKm.HasValue;
        if (anyLocation && !allLocation)
        {
            errors.Add("latitude", "Latitude, longitude and radius_km must be supplied together.");
        }

        if (query.Latitude is { } latitude && latitude is < -90 or > 90)
        {
            errors.Add("latitude", "Latitude must be between -90 and 90.");
        }

        if (query.Longitude is { } longitude && longitude is < -180 or > 180)
        {
            errors.Add("longitude", "Longitude must be between -180 and 180.");
        }

        if (query.RadiusKm is { } radius && (radius <= 0 || radius > MaximumRadiusKm))
        {
            errors.Add("radius_km", $"Radius must be greater than 0 and at most {MaximumRadiusKm} km.");
        }

        errors.ThrowIfAny();
    }

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static NotFoundException RequestNotFound() => new("Play request not found.");
}
