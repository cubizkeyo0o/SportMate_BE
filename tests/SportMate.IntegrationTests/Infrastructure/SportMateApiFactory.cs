using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SportMate.Domain.Entities;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API pipeline against a shared SQLite in-memory database created from the EF model,
/// so unique indexes, foreign keys and check constraints are enforced. <see cref="MySqlApiFactory"/>
/// swaps the database for a real MySQL one where row locking matters.
/// </summary>
public class SportMateApiFactory : WebApplicationFactory<Program>
{
    // Test-only key; real keys come from the Jwt__SigningKey environment variable.
    public const string SigningKey = "integration-tests-only-signing-key-0123456789";
    public const string Issuer = "SportMate";
    public const string Audience = "SportMate";

    private readonly SqliteConnection connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SportMateDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SportMateDbContext>>();
            ConfigureDatabase(services);
            services.AddControllers().AddApplicationPart(typeof(RoleProbeController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        PrepareDatabase();
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        InitializeDatabase(scope.ServiceProvider.GetRequiredService<SportMateDbContext>());
        return host;
    }

    protected virtual void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<SportMateDbContext>(options => options.UseSqlite(connection));

    protected virtual void PrepareDatabase()
    {
        connection.Open();

        // Stand-in for the MySQL case-insensitive collation used on users.email and sports.name.
        connection.CreateCollation(
            "utf8mb4_0900_ai_ci",
            (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual void InitializeDatabase(SportMateDbContext db) => db.Database.EnsureCreated();

    public async Task WithDbAsync(Func<SportMateDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<SportMateDbContext>());
    }

    public Task ChangeRoleAsync(Guid userId, UserRole role) => WithDbAsync(async db =>
    {
        (await db.Users.SingleAsync(u => u.Id == userId)).ChangeRole(role, DateTime.UtcNow);
        await db.SaveChangesAsync();
    });

    public Task SuspendAsync(Guid userId) => WithDbAsync(async db =>
    {
        (await db.Users.SingleAsync(u => u.Id == userId)).Suspend(DateTime.UtcNow);
        await db.SaveChangesAsync();
    });

    /// <summary>
    /// Adds a uniquely named sport, so tests can deactivate it without touching the shared seed data.
    /// </summary>
    public async Task<Sport> AddSportAsync(bool isActive = true, string? name = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var sport = new Sport(name ?? $"Test Sport {suffix}", $"test-sport-{suffix}", DateTime.UtcNow);
        sport.SetActive(isActive, DateTime.UtcNow);
        await WithDbAsync(async db =>
        {
            db.Sports.Add(sport);
            await db.SaveChangesAsync();
        });
        return sport;
    }

    public Task SetSportActiveAsync(Guid sportId, bool isActive) => WithDbAsync(async db =>
    {
        (await db.Sports.SingleAsync(s => s.Id == sportId)).SetActive(isActive, DateTime.UtcNow);
        await db.SaveChangesAsync();
    });

    /// <summary>
    /// Moves a booking in time (tests cannot wait for holds to expire or bookings to end). Writes the
    /// columns directly, bypassing the domain, which never lets these values change.
    /// </summary>
    public Task SetBookingTimesAsync(
        Guid bookingId,
        DateTime? createdAtUtc = null,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null) => WithDbAsync(async db =>
    {
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        var entry = db.Entry(booking);
        if (createdAtUtc is not null)
        {
            entry.Property(b => b.CreatedAtUtc).CurrentValue = createdAtUtc.Value;
        }

        if (startsAtUtc is not null)
        {
            entry.Property(b => b.StartsAtUtc).CurrentValue = startsAtUtc.Value;
        }

        if (endsAtUtc is not null)
        {
            entry.Property(b => b.EndsAtUtc).CurrentValue = endsAtUtc.Value;
        }

        await db.SaveChangesAsync();
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
