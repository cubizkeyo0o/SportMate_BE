using Microsoft.EntityFrameworkCore;
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
