using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SportMate.Domain.Enums;
using SportMate.Infrastructure.Persistence;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API pipeline against a shared SQLite in-memory database created from the EF model,
/// so unique indexes, foreign keys and check constraints are enforced.
/// </summary>
public sealed class SportMateApiFactory : WebApplicationFactory<Program>
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
            services.AddDbContext<SportMateDbContext>(options => options.UseSqlite(connection));
            services.AddControllers().AddApplicationPart(typeof(RoleProbeController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        connection.Open();

        // Stand-in for the MySQL case-insensitive collation used on users.email and sports.name.
        connection.CreateCollation(
            "utf8mb4_0900_ai_ci",
            (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));

        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SportMateDbContext>().Database.EnsureCreated();
        return host;
    }

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
