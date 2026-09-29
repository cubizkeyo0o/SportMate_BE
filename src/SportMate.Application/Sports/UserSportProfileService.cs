using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Entities;

namespace SportMate.Application.Sports;

/// <summary>
/// The authenticated user's sport profiles. <c>userId</c> always comes from the access token
/// (already checked to be an existing, active user), never from the request.
/// </summary>
public sealed class UserSportProfileService(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<UserSportProfileResponse>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserSportProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Sport.Name)
            .ThenBy(p => p.SportId)
            .Select(p => new UserSportProfileResponse(
                new ProfileSportResponse(p.Sport.Id, p.Sport.Name, p.Sport.Slug, p.Sport.IsActive),
                p.SkillLevel))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Creates the profile or changes its skill level. A new profile needs an active sport; an existing
    /// profile stays editable after its sport is deactivated.
    /// </summary>
    public async Task<UserSportProfileResponse> SetAsync(
        Guid userId,
        Guid sportId,
        SetSportProfileRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var skillLevel = SportProfileInputRules.SkillLevel(request.SkillLevel, errors);
        errors.ThrowIfAny();

        var sport = await db.Sports.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sportId, cancellationToken)
            ?? throw new NotFoundException("Sport not found.");

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var profile = await db.UserSportProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.SportId == sportId, cancellationToken);

        if (profile is null)
        {
            if (!sport.IsActive)
            {
                errors.Add("sport_id", "This sport is no longer available for new profiles.");
                errors.ThrowIfAny();
            }

            db.UserSportProfiles.Add(new UserSportProfile(userId, sportId, skillLevel, utcNow));
        }
        else
        {
            profile.ChangeSkillLevel(skillLevel, utcNow);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (profile is null)
        {
            // A concurrent request created the same (user, sport) profile first; the unique index rejected ours.
            throw new ConflictException("The sport profile was changed by another request. Please retry.");
        }

        return new UserSportProfileResponse(
            new ProfileSportResponse(sport.Id, sport.Name, sport.Slug, sport.IsActive),
            skillLevel);
    }

    /// <summary>
    /// Idempotent: removing a profile that does not exist (or for an unknown sport) succeeds.
    /// Never touches the sport itself.
    /// </summary>
    public Task RemoveAsync(Guid userId, Guid sportId, CancellationToken cancellationToken) =>
        db.UserSportProfiles
            .Where(p => p.UserId == userId && p.SportId == sportId)
            .ExecuteDeleteAsync(cancellationToken);
}
