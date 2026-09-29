using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SportMate.Application.Common;

namespace SportMate.API.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new AuthenticationFailedException("The access token does not identify a user.");
}
