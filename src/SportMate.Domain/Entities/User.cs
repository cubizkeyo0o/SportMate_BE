using SportMate.Domain.Common;
using SportMate.Domain.Enums;

namespace SportMate.Domain.Entities;

public sealed class User : Entity
{
    public const int EmailMaxLength = 254;
    public const int PasswordHashMaxLength = 255;
    public const int DisplayNameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int AvatarUrlMaxLength = 500;

    private User()
    {
    }

    public User(string email, string passwordHash, string displayName, UserRole role, DateTime utcNow)
    {
        Email = Guard.NotBlank(email, EmailMaxLength, nameof(email));
        PasswordHash = Guard.NotBlank(passwordHash, PasswordHashMaxLength, nameof(passwordHash));
        DisplayName = Guard.NotBlank(displayName, DisplayNameMaxLength, nameof(displayName));
        Role = role;
        Status = UserStatus.Active;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Stored trimmed; uniqueness is case-insensitive via the column collation.
    /// </summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string? PhoneNumber { get; private set; }

    public string? AvatarUrl { get; private set; }

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    public ICollection<UserSportProfile> SportProfiles { get; private set; } = new List<UserSportProfile>();

    public void UpdateProfile(string displayName, string? phoneNumber, string? avatarUrl, DateTime utcNow)
    {
        DisplayName = Guard.NotBlank(displayName, DisplayNameMaxLength, nameof(displayName));
        PhoneNumber = Guard.Optional(phoneNumber, PhoneNumberMaxLength, nameof(phoneNumber));
        AvatarUrl = Guard.Optional(avatarUrl, AvatarUrlMaxLength, nameof(avatarUrl));
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void ChangePasswordHash(string passwordHash, DateTime utcNow)
    {
        PasswordHash = Guard.NotBlank(passwordHash, PasswordHashMaxLength, nameof(passwordHash));
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void ChangeRole(UserRole role, DateTime utcNow)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("Invalid role.");
        }

        Role = role;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void Suspend(DateTime utcNow)
    {
        Status = UserStatus.Suspended;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public void Activate(DateTime utcNow)
    {
        Status = UserStatus.Active;
        UpdatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }
}
