using Microsoft.EntityFrameworkCore;

namespace SportMate.Application.Sports;

/// <summary>
/// Read-only access to the fixed sport catalog. There is no sport management API.
/// </summary>
public sealed class SportService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<SportResponse>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Sports
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new SportResponse(s.Id, s.Name, s.Slug))
            .ToListAsync(cancellationToken);
}
