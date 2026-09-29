using Microsoft.EntityFrameworkCore;
using SportMate.Application.Common;
using SportMate.Application.Users;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;

namespace SportMate.Application.Auth;

public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    JwtOptions jwtOptions,
    TimeProvider timeProvider)
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var email = UserInputRules.Email(request.Email, errors);
        UserInputRules.NewPassword(request.Password, errors);
        var displayName = UserInputRules.DisplayName(request.DisplayName, errors);
        var phone = UserInputRules.Phone(request.Phone, errors);
        errors.ThrowIfAny();

        if (await EmailExistsAsync(email, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var now = UtcNow();
        var user = new User(email, passwordHasher.Hash(request.Password!), displayName, UserRole.User, now);
        user.UpdateProfile(displayName, phone, avatarUrl: null, now);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index on users.email is the source of truth when two registrations race.
            if (await EmailExistsAsync(email, cancellationToken))
            {
                throw DuplicateEmail();
            }

            throw;
        }

        return UserResponse.From(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var email = UserInputRules.NormalizeEmail(request.Email);
        if (email.Length == 0)
        {
            errors.Add("email", "Email is required.");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add("password", "Password is required.");
        }

        errors.ThrowIfAny();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            // Spend comparable time to a real verification so response timing does not reveal the account exists.
            passwordHasher.Hash(request.Password!);
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        var verification = passwordHasher.Verify(user.PasswordHash, request.Password!);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        // Checked only after the password is proven, so it does not disclose anything to a guesser.
        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("This account is not active.");
        }

        var now = UtcNow();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password!), now);
        }

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and a new pair is issued.
    /// Presenting an already-revoked token is treated as theft and revokes all of the user's tokens.
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var rawToken = RequireRefreshToken(request);
        var tokenHash = RefreshTokenGenerator.Hash(rawToken);
        var now = UtcNow();

        var token = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (token is null)
        {
            throw new AuthenticationFailedException(InvalidRefreshTokenMessage);
        }

        if (token.RevokedAtUtc is not null)
        {
            await RevokeAllAsync(token.UserId, now, cancellationToken);
            throw new AuthenticationFailedException(InvalidRefreshTokenMessage);
        }

        if (!token.IsActive(now) || token.User.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException(InvalidRefreshTokenMessage);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Conditional update so two concurrent refreshes with the same token cannot both succeed.
        var revoked = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now), cancellationToken);
        if (revoked == 0)
        {
            throw new AuthenticationFailedException(InvalidRefreshTokenMessage);
        }

        var response = await IssueTokensAsync(token.User, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    /// <summary>
    /// Revokes the given refresh token if it belongs to the user. Idempotent.
    /// </summary>
    public async Task LogoutAsync(Guid userId, RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenGenerator.Hash(RequireRefreshToken(request));
        var now = UtcNow();

        await db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now), cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        var rawRefreshToken = RefreshTokenGenerator.Generate();
        db.RefreshTokens.Add(new RefreshToken(
            user.Id,
            RefreshTokenGenerator.Hash(rawRefreshToken),
            now.AddDays(jwtOptions.RefreshTokenDays),
            now));
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenService.Create(user, now);
        return new AuthResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            (int)(accessToken.ExpiresAtUtc - now).TotalSeconds,
            UserResponse.From(user));
    }

    private Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now), cancellationToken);

    private static string RequireRefreshToken(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var errors = new ValidationErrors();
            errors.Add("refresh_token", "Refresh token is required.");
            errors.ThrowIfAny();
        }

        return request.RefreshToken!.Trim();
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    private static ConflictException DuplicateEmail() => new("An account with this email already exists.");

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
