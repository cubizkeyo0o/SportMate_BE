using SportMate.Domain.Enums;

namespace SportMate.Application.Auth;

/// <summary>
/// Role names used in the access token and in <c>[Authorize(Roles = ...)]</c>. They are the
/// <see cref="UserRole"/> member names. Use plain <c>[Authorize]</c> for normal user functionality,
/// which every role (including CourtOwner and Admin) may use.
/// </summary>
public static class Roles
{
    /// <summary>
    /// JWT claim carrying the role name. The role is re-read from the database on every request.
    /// </summary>
    public const string ClaimType = "role";

    public const string User = nameof(UserRole.User);
    public const string CourtOwner = nameof(UserRole.CourtOwner);
    public const string Admin = nameof(UserRole.Admin);

    public static string NameOf(UserRole role) => role.ToString();
}
