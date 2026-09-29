using SportMate.Domain.Common;

namespace SportMate.Domain.Entities;

public sealed class Sport : Entity
{
    public const int NameMaxLength = 50;

    private Sport()
    {
    }

    public Sport(string name, DateTime utcNow)
    {
        Name = Guard.NotBlank(name, NameMaxLength, nameof(name));
        IsActive = true;
        CreatedAtUtc = Guard.Utc(utcNow, nameof(utcNow));
    }

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
