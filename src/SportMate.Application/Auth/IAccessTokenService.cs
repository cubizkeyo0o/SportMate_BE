using SportMate.Domain.Entities;

namespace SportMate.Application.Auth;

public interface IAccessTokenService
{
    AccessToken Create(User user, DateTime utcNow);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
