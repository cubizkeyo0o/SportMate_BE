using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using SportMate.Application.Auth;
using SportMate.Application.Users;

namespace SportMate.API.Authentication;

/// <summary>
/// Makes the database authoritative for every accepted access token: tokens of missing or
/// suspended users are rejected (401), and the role claim is replaced with the user's current role
/// so role changes take effect without waiting for the token to expire.
/// </summary>
public sealed class CurrentUserJwtBearerEvents(UserService userService) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity
            || !Guid.TryParse(identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            context.Fail("The access token does not identify a user.");
            return;
        }

        var role = await userService.FindActiveUserRoleAsync(userId, context.HttpContext.RequestAborted);
        if (role is null)
        {
            context.Fail("The user does not exist or is not active.");
            return;
        }

        foreach (var tokenRole in identity.FindAll(Roles.ClaimType).ToList())
        {
            identity.RemoveClaim(tokenRole);
        }

        identity.AddClaim(new Claim(Roles.ClaimType, Roles.NameOf(role.Value)));
    }
}
