namespace SportMate.Application.Auth;

/// <summary>
/// Bound from the "Jwt" configuration section. The signing key must come from
/// environment variables or user secrets, never from committed configuration.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 needs at least 256 bits of key material.
    /// </summary>
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; set; } = "SportMate";

    public string Audience { get; set; } = "SportMate";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}
