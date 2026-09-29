using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Users;

public sealed class UserService(IApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Returns the user's current role if the user exists and is active; otherwise null.
    /// Called for every request carrying a valid access token, so the database state
    /// (suspension, role changes) overrides whatever the token says.
    /// </summary>
    public async Task<UserRole?> FindActiveUserRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        return user is { Status: UserStatus.Active } ? user.Role : null;
    }

    public async Task<UserResponse> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        return UserResponse.From(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var displayName = UserInputRules.DisplayName(request.DisplayName, errors);
        var phone = UserInputRules.Phone(request.Phone, errors);
        errors.ThrowIfAny();

        var user = await GetActiveUserAsync(userId, cancellationToken);
        user.UpdateProfile(displayName, phone, user.AvatarUrl, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return UserResponse.From(user);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // The access token may outlive the account; re-check on every call.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AuthenticationFailedException("The user no longer exists.");

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("This account is not active.");
        }

        return user;
    }
}
