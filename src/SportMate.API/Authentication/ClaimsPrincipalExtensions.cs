using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SportMate.Application.Auth;
using SportMate.Application.Common;
using SportMate.Domain.Enums;

namespace SportMate.API.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new AuthenticationFailedException("The access token does not identify a user.");

    /// <summary>
    /// The caller's current role. <see cref="CurrentUserJwtBearerEvents"/> replaces the token's role claim
    /// with the database value, so this is never a stale or client-chosen role.
    /// </summary>
    public static UserRole GetUserRole(this ClaimsPrincipal principal) =>
        Enum.TryParse<UserRole>(principal.FindFirstValue(Roles.ClaimType), out var role) && Enum.IsDefined(role)
            ? role
            : throw new AuthenticationFailedException("The access token does not carry a valid role.");
}
