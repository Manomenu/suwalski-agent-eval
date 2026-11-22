using Npgsql;
using Suwbackkit.Postgres;
using Suwbackkit.Testing;

[assembly: AssemblyFixture(typeof(Suwageval.Server.Tests.TestDatabase))]

namespace Suwageval.Server.Tests;

public sealed class TestDatabase : IAsyncLifetime
{
    private const string MainCheckoutConnectionString =
        "Host=127.0.0.1;Port=5463;Database=suwageval_test;Username=suwageval;Password=suwageval";

    public static readonly string ConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? MainCheckoutConnectionString;

    private string? unavailable;
    private NpgsqlDataSource? dataSource;

    internal NpgsqlDataSource DataSource
    {
        get
        {
            if (unavailable is not null)
            {
                Assert.Skip($"PostgreSQL unavailable — start it with `just db up` ({unavailable})");
            }
            return dataSource!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        LinkedWorktree.RefuseSharedDatabase();
        try
        {
            await TestDatabases.RecreateAsync(ConnectionString);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
            return;
        }
        dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(ConnectionString) { MaxPoolSize = 4 }.ConnectionString);
        await Migrator.MigrateAsync(ConnectionString, Migrator.DefaultDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        if (dataSource is not null)
        {
            await dataSource.DisposeAsync();
        }
    }
}
