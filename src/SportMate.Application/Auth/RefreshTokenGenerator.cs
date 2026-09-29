using System.Security.Cryptography;
using System.Text;

namespace SportMate.Application.Auth;

public static class RefreshTokenGenerator
{
    private const int TokenBytes = 32;

    /// <summary>
    /// Creates a cryptographically random, URL-safe token. Returned to the client only once.
    /// </summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Lower-case hex SHA-256 of the raw token; the only form persisted.
    /// </summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
