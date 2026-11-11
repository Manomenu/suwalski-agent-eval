using Npgsql;
using Suwageval.Server.Database;

[assembly: AssemblyFixture(typeof(Suwageval.Server.Tests.TestDatabase))]

namespace Suwageval.Server.Tests;

public sealed class TestDatabase : IAsyncLifetime
{
    private const string MainCheckoutConnectionString =
        "Host=localhost;Port=5463;Database=suwageval_test;Username=suwageval;Password=suwageval";

    private static readonly string? FromEnvironment = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");

    public static readonly string ConnectionString = FromEnvironment ?? MainCheckoutConnectionString;

    private string? unavailable;
    private Pool? pool;

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
        RefuseMainCheckoutDatabaseInLinkedWorktree(FromEnvironment, AppContext.BaseDirectory);
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

    // Why: the fixture drops and recreates its database, so a worktree on the main checkout's name wipes the tests running there
    internal static void RefuseMainCheckoutDatabaseInLinkedWorktree(string? fromEnvironment, string testDirectory)
    {
        if (fromEnvironment is not null || !IsInLinkedWorktree(testDirectory))
        {
            return;
        }
        throw new InvalidOperationException(
            "TEST_CONNECTION_STRING is not set, and this is a linked git worktree, so the tests would use the main checkout's "
                + "database suwageval_test: run them after 'source scripts/.internal/checkout-env.sh' (bash or zsh), "
                + "or run Suwageval.Gate/scripts/check.sh"
        );
    }

    // Why: a linked worktree has a .git file pointing into the main repository's .git/worktrees/, the main checkout a .git directory
    private static bool IsInLinkedWorktree(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var git = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(git))
            {
                return false;
            }
            if (File.Exists(git))
            {
                return File.ReadAllText(git).Replace('\\', '/').Contains("/worktrees/", StringComparison.Ordinal);
            }
        }
        return false;
    }

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
