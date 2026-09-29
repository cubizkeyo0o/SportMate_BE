using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

public sealed class RefreshToken : Entity
{
    /// <summary>
    /// Hex-encoded SHA-256 of the raw token. The raw token is never stored.
    /// </summary>
    public const int TokenHashLength = 64;

    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime utcNow)
    {
        if (tokenHash is null || tokenHash.Length != TokenHashLength)
        {
            throw new DomainException($"Token hash must be {TokenHashLength} characters.");
        }

        Guard.UtcRange(utcNow, expiresAtUtc);
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = utcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;

    public void Revoke(DateTime utcNow)
    {
        RevokedAtUtc ??= Guard.Utc(utcNow, nameof(utcNow));
    }
}
