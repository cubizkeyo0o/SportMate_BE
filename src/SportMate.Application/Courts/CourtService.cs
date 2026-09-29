using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Courts;

/// <summary>
/// Court management. <c>actorId</c> and <c>actorRole</c> identify the authenticated caller; they come
/// from the access token after it has been checked against the database (the user exists, is active,
/// and the role is the current one), never from the request body.
/// </summary>
public sealed class CourtService(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<CourtResponse> CreateAsync(
        UserRole actorRole,
        CreateCourtRequest request,
        CancellationToken cancellationToken)
    {
        if (actorRole != UserRole.Admin)
        {
            throw new ForbiddenException("Only an Admin can create courts.");
        }

        var errors = new ValidationErrors();
        var name = CourtInputRules.Name(request.Name, errors);
        var description = CourtInputRules.Description(request.Description, errors);
        var address = CourtInputRules.Address(request.Address, errors);
        var city = CourtInputRules.City(request.City, errors);
        var latitude = CourtInputRules.Latitude(request.Latitude, errors);
        var longitude = CourtInputRules.Longitude(request.Longitude, errors);
        var timeZoneId = CourtInputRules.TimeZoneId(request.TimeZoneId, errors);
        if (request.OwnerId is null)
        {
            errors.Add("owner_id", "Owner is required.");
        }
        else
        {
            await ValidateOwnerAsync(request.OwnerId.Value, errors, cancellationToken);
        }

        errors.ThrowIfAny();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var court = new Court(
            request.OwnerId!.Value, name, description, address, city, latitude, longitude, timeZoneId, utcNow);
        db.Courts.Add(court);
        await db.SaveChangesAsync(cancellationToken);
        return CourtResponse.From(court);
    }

    public async Task<CourtResponse> GetAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken)
    {
        var court = await db.Courts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courtId, cancellationToken)
            ?? throw new NotFoundException("Court not found.");

        CourtAccess.EnsureCanManage(court.OwnerId, actorId, actorRole);
        return CourtResponse.From(court);
    }

    public async Task<CourtResponse> UpdateAsync(
        Guid courtId,
        Guid actorId,
        UserRole actorRole,
        UpdateCourtRequest request,
        CancellationToken cancellationToken)
    {
        var court = await db.Courts.FirstOrDefaultAsync(c => c.Id == courtId, cancellationToken)
            ?? throw new NotFoundException("Court not found.");

        CourtAccess.EnsureCanManage(court.OwnerId, actorId, actorRole);

        var changesOwner = request.OwnerId is { } requestedOwner && requestedOwner != court.OwnerId;
        if (changesOwner && actorRole != UserRole.Admin)
        {
            throw new ForbiddenException("Only an Admin can change the owner of a court.");
        }

        var errors = new ValidationErrors();
        var name = CourtInputRules.Name(request.Name, errors);
        var description = CourtInputRules.Description(request.Description, errors);
        var address = CourtInputRules.Address(request.Address, errors);
        var city = CourtInputRules.City(request.City, errors);
        var latitude = CourtInputRules.Latitude(request.Latitude, errors);
        var longitude = CourtInputRules.Longitude(request.Longitude, errors);
        var timeZoneId = CourtInputRules.TimeZoneId(request.TimeZoneId, errors);
        var status = CourtInputRules.Status(request.Status, errors);
        if (changesOwner)
        {
            await ValidateOwnerAsync(request.OwnerId!.Value, errors, cancellationToken);
        }

        errors.ThrowIfAny();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        court.UpdateDetails(name, description, address, city, latitude, longitude, timeZoneId, utcNow);
        court.SetStatus(status, utcNow);
        if (changesOwner)
        {
            court.AssignOwner(request.OwnerId!.Value, utcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        return CourtResponse.From(court);
    }

    private async Task ValidateOwnerAsync(Guid ownerId, ValidationErrors errors, CancellationToken cancellationToken)
    {
        var owner = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerId)
            .Select(u => new { u.Status, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (owner is null)
        {
            errors.Add("owner_id", "Owner was not found.");
        }
        else if (owner.Status != UserStatus.Active)
        {
            errors.Add("owner_id", "Owner account is not active.");
        }
        else if (owner.Role != UserRole.CourtOwner)
        {
            errors.Add("owner_id", "Owner must be a user with the CourtOwner role.");
        }
    }
}
