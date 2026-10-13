using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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

    DbSet<CourtSport> CourtSports { get; }

    DbSet<CourtOperatingHour> CourtOperatingHours { get; }

    DbSet<Sport> Sports { get; }

    DbSet<UserSportProfile> UserSportProfiles { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<PlayRequest> PlayRequests { get; }

    DbSet<PlayRequestParticipant> PlayRequestParticipants { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an exclusive row lock on the court (MySQL: <c>SELECT ... FOR UPDATE</c>) that lasts until the
    /// current transaction ends. This is the serialization point for booking changes on one physical court.
    /// Must be called inside a transaction.
    /// </summary>
    Task LockCourtForUpdateAsync(Guid courtId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an exclusive row lock on a play request until the current transaction ends. Must be called inside
    /// a transaction before changing the request or its participants.
    /// </summary>
    Task LockPlayRequestForUpdateAsync(Guid playRequestId, CancellationToken cancellationToken);
}
