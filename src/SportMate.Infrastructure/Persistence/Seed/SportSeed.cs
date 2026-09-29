namespace SportMate.Infrastructure.Persistence.Seed;

/// <summary>
/// The fixed sport catalog (docs/BUSINESS_RULES.md section 3). IDs, slugs and timestamps are stable:
/// clients and other rows reference them, so never change or reuse them. Deactivate instead of removing.
/// </summary>
public static class SportSeed
{
    public static readonly DateTime SeededAtUtc = new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<(Guid Id, string Name, string Slug)> Sports =
    [
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000001"), "Tennis", "tennis"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000002"), "Badminton", "badminton"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000003"), "Basketball", "basketball"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000004"), "Football", "football"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000005"), "Pickleball", "pickleball"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000006"), "Table Tennis", "table-tennis"),
        (new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000007"), "Volleyball", "volleyball")
    ];
}
