using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportMate.Application;
using SportMate.Domain.Entities;
using SportMate.Infrastructure.Persistence.Conventions;

namespace SportMate.Infrastructure.Persistence;

public sealed class SportMateDbContext(DbContextOptions<SportMateDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Sport> Sports => Set<Sport>();

    public DbSet<UserSportProfile> UserSportProfiles => Set<UserSportProfile>();

    public DbSet<Court> Courts => Set<Court>();

    public DbSet<CourtSport> CourtSports => Set<CourtSport>();

    public DbSet<CourtOperatingHour> CourtOperatingHours => Set<CourtOperatingHour>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<PlayRequest> PlayRequests => Set<PlayRequest>();

    public DbSet<PlayRequestParticipant> PlayRequestParticipants => Set<PlayRequestParticipant>();

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public async Task LockCourtForUpdateAsync(Guid courtId, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The court row can only be locked inside a transaction.");
        }

        // SQLite (used only by the integration tests) has no row locks; it serializes writers itself.
        if (Database.IsMySql())
        {
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM courts WHERE id = {courtId} FOR UPDATE",
                cancellationToken);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>().HaveColumnType("char(36)");
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("datetime(6)")
            .HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<TimeOnly>().HaveColumnType("time");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SportMateDbContext).Assembly);
        modelBuilder.ApplySnakeCaseNames();
    }
}
