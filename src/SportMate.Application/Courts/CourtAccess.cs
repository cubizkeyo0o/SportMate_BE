using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// The single court-management permission check. Admin manages every court; a CourtOwner manages only
/// courts whose stored owner is the caller; nobody else manages courts. <c>actorId</c> and
/// <c>actorRole</c> come from the access token after it was checked against the database (existing,
/// active user, current role), so a demoted or suspended owner loses access immediately.
/// </summary>
internal static class CourtAccess
{
    public static void EnsureCanManage(Guid courtOwnerId, Guid actorId, UserRole actorRole)
    {
        var allowed = actorRole switch
        {
            UserRole.Admin => true,
            UserRole.CourtOwner => courtOwnerId == actorId,
            _ => false
        };

        if (!allowed)
        {
            throw new ForbiddenException("You do not have permission to manage this court.");
        }
    }

    /// <summary>
    /// 404 if the court does not exist, 403 if the caller may not manage it.
    /// </summary>
    public static async Task EnsureCanManageAsync(
        IApplicationDbContext db,
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        var ownerId = await db.Courts
            .Where(c => c.Id == courtId)
            .Select(c => (Guid?)c.OwnerId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Court not found.");

        EnsureCanManage(ownerId, actorId, actorRole);
    }
}
