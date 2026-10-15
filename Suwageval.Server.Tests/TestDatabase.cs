using Npgsql;
using Suwageval.Server.Database;

[assembly: AssemblyFixture(typeof(Suwageval.Server.Tests.TestDatabase))]

namespace Suwageval.Server.Tests;

/// <summary>
/// A real PostgreSQL for the tests that need one. The tests get a database of their own, created afresh for
/// every run next to the development one, so running them never touches data you made by hand. Locally a
/// missing server skips those tests with a hint; in CI (where <c>CI</c> is set) it fails them, because there
/// it must exist.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5463;Database=suwageval_test;Username=suwageval;Password=suwageval";

    private string? unavailable;
    private Pool? pool;

    /// <summary>The pool on the test database — or the test is skipped (failed in CI) with the reason.</summary>
    internal Pool Pool
    {
        get
        {
            if (unavailable is not null)
            {
                Assert.Skip($"PostgreSQL unavailable — start it with `just db up` ({unavailable})");
            }
            return pool!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await RecreateAsync(ConnectionString);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
            return;
        }
        pool = Pool.Create(ConnectionString, maxSize: 4);
        await Migrator.MigrateAsync(ConnectionString, Migrator.DefaultDirectory);
    }

    /// <summary>Drops the database named in the connection string (if it exists) and creates it empty.</summary>
    public static async Task RecreateAsync(string connectionString)
    {
        await DropAsync(connectionString);
        await using var connection = await OpenAdminAsync(connectionString);
        await using var create = new NpgsqlCommand(
            $"CREATE DATABASE \"{new NpgsqlConnectionStringBuilder(connectionString).Database}\"",
            connection
        );
        await create.ExecuteNonQueryAsync();
    }

    public static async Task DropAsync(string connectionString)
    {
        await using var connection = await OpenAdminAsync(connectionString);
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{new NpgsqlConnectionStringBuilder(connectionString).Database}\" WITH (FORCE)",
            connection
        );
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task<NpgsqlConnection> OpenAdminAsync(string connectionString)
    {
        var admin = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
            Timeout = 3,
            Pooling = false,
        };
        var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (pool is not null)
        {
            await pool.DisposeAsync();
        }
    }
}
