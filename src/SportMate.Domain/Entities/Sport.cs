using System.Text.RegularExpressions;
using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

/// <summary>
/// A system-level sport. The catalog is fixed seed data; sports are deactivated, never deleted.
/// </summary>
public sealed partial class Sport : Entity
{
    public const int NameMaxLength = 50;
    public const int SlugMaxLength = 50;

    private Sport()
    {
    }

    public Sport(string name, string slug, DateTime utcNow)
    {
        Name = Guard.NotBlank(name, NameMaxLength, nameof(name));
        Slug = ValidSlug(slug);
        IsActive = true;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
        UpdatedAtUtc = utcNow;
    }

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Lowercase, hyphen-separated identifier, e.g. "table-tennis".
    /// </summary>
    public string Slug { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Deactivating keeps every existing reference (profiles, court sports, bookings, play requests).
    /// </summary>
    public void SetActive(bool isActive, DateTime utcNow)
    {
        Guard.Utc(utcNow, nameof(utcNow));
        if (isActive == IsActive)
        {
            return;
        }

        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private static string ValidSlug(string slug)
    {
        var value = Guard.NotBlank(slug, SlugMaxLength, nameof(slug));
        return SlugPattern().IsMatch(value)
            ? value
            : throw new DomainException("Slug must be lowercase letters and digits separated by single hyphens.");
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
