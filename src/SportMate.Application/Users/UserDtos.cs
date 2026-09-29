using SportMate.Application.Auth;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Users;

/// <param name="Role">One of <see cref="Roles.User"/>, <see cref="Roles.CourtOwner"/>, <see cref="Roles.Admin"/>.</param>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? Phone,
    UserStatus Status,
    string Role)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.PhoneNumber, user.Status, Roles.NameOf(user.Role));
}

/// <summary>
/// Full replacement of the editable profile fields; omitting phone clears it.
/// </summary>
public sealed record UpdateProfileRequest(string? DisplayName, string? Phone);
