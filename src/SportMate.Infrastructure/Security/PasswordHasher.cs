using SportMate.Application.Auth;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<SportMate.Domain.Entities.User>;
using IdentityVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace SportMate.Infrastructure.Security;

/// <summary>
/// Delegates to ASP.NET Core Identity's PBKDF2 hasher (salted, versioned hash format).
/// The Identity hasher never reads the user argument, so null is passed.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityPasswordHasher hasher = new();

    public string Hash(string password) => hasher.HashPassword(null!, password);

    public PasswordVerificationResult Verify(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(null!, passwordHash, password) switch
        {
            IdentityVerificationResult.Success => PasswordVerificationResult.Success,
            IdentityVerificationResult.SuccessRehashNeeded => PasswordVerificationResult.SuccessRehashNeeded,
            _ => PasswordVerificationResult.Failed
        };
}
