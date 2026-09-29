using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SportMate.Application.Auth;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Security;

internal sealed class JwtAccessTokenService(JwtOptions options) : IAccessTokenService
{
    private readonly JsonWebTokenHandler handler = new();

    private readonly SigningCredentials signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken Create(User user, DateTime utcNow)
    {
        var expiresAtUtc = utcNow.AddMinutes(options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAtUtc,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [Roles.ClaimType] = Roles.NameOf(user.Role),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            },
            SigningCredentials = signingCredentials
        };

        return new AccessToken(handler.CreateToken(descriptor), expiresAtUtc);
    }
}
