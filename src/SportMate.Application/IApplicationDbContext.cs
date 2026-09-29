using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SportMate.Domain.Entities;

namespace SportMate.Application;

/// <summary>
/// The persistence surface used by application services. Implemented by the EF Core DbContext.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Court> Courts { get; }

    DbSet<Sport> Sports { get; }

    DbSet<UserSportProfile> UserSportProfiles { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
