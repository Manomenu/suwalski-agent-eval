using AgentEval.Server.Db;
using Npgsql;

[assembly: AssemblyFixture(typeof(AgentEval.Server.Tests.TestDatabase))]

namespace AgentEval.Server.Tests;

/// <summary>
/// A real PostgreSQL for the tests that need one. The tests get a database of their own, created afresh for
/// every run next to the development one, so running them never touches data you made by hand. Locally a
/// missing server skips those tests with a hint; in CI (where <c>CI</c> is set) it fails them, because there
/// it must exist.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public static readonly string DatabaseUrl =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL") ?? "postgresql://agenteval:agenteval@localhost:5453/agenteval_test";

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
        var test = new NpgsqlConnectionStringBuilder(Database.ToConnectionString(DatabaseUrl));
        var admin = new NpgsqlConnectionStringBuilder(test.ConnectionString)
        {
            Database = "postgres",
            Timeout = 3,
            Pooling = false,
        };
        try
        {
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{test.Database}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{test.Database}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
            return;
        }
        pool = Pool.Create(DatabaseUrl, maxSize: 4);
        await using var migrating = await pool.OpenAsync();
        await Migrations.MigrateAsync(migrating, Migrations.DefaultDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        if (pool is not null)
        {
            await pool.DisposeAsync();
        }
    }
}
