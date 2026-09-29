using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SportMate.Infrastructure.Persistence;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// The API against a real, throwaway MySQL database (<c>sm_test_&lt;random&gt;</c>) built with the real
/// migrations, for behavior SQLite cannot reproduce such as InnoDB row locks. The server connection string
/// comes only from the <see cref="ConnectionStringVariable"/> environment variable; the account needs
/// CREATE and DROP DATABASE. The database is dropped when the factory is disposed.
/// </summary>
public sealed class MySqlApiFactory : SportMateApiFactory
{
    public const string ConnectionStringVariable = "SPORTMATE_TEST_MYSQL";

    // Short: EF's migration lock name embeds it, and MySQL lock names are limited to 64 characters.
    private readonly string databaseName = $"sm_test_{Guid.NewGuid().ToString("N")[..12]}";
    private string? serverConnectionString;

    /// <summary>
    /// Connection string of the test database (no credentials are ever written to disk or output).
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    protected override void PrepareDatabase()
    {
        if (serverConnectionString is not null)
        {
            return; // already created; xUnit may build the host again after a failure
        }

        var configured = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException($"{ConnectionStringVariable} is not set.");
        }

        var server = new MySqlConnectionStringBuilder(configured) { Database = string.Empty };
        serverConnectionString = server.ConnectionString;
        ConnectionString = new MySqlConnectionStringBuilder(configured) { Database = databaseName }.ConnectionString;

        using var connection = new MySqlConnection(serverConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
        command.ExecuteNonQuery();
    }

    // The options callback runs per DbContext, after PrepareDatabase has set ConnectionString.
    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<SportMateDbContext>(options =>
            options.UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 16))));

    protected override void InitializeDatabase(SportMateDbContext db) => db.Database.Migrate();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && serverConnectionString is not null)
        {
            MySqlConnection.ClearAllPools();
            using var connection = new MySqlConnection(serverConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`";
            command.ExecuteNonQuery();
        }
    }
}

/// <summary>
/// A test that needs real MySQL. Reported as skipped (never as passed) when
/// <see cref="MySqlApiFactory.ConnectionStringVariable"/> is not set.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlApiFactory.IsConfigured)
        {
            Skip = $"Set {MySqlApiFactory.ConnectionStringVariable} to a MySQL connection string to run this test.";
        }
    }
}
