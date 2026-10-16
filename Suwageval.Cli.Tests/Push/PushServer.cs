using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Suwageval.Server.Api;

[assembly: AssemblyFixture(typeof(Suwageval.Cli.Tests.Push.PushServer))]

namespace Suwageval.Cli.Tests.Push;

/// <summary>
/// The real server in-process on a PostgreSQL database of its own (created afresh per run, next to the server tests' one, so
/// the two test projects can run in parallel). Locally a missing PostgreSQL skips the tests; in CI it fails them.
/// </summary>
public sealed class PushServer : IAsyncLifetime
{
    private static readonly string ConnectionString = new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? "Host=localhost;Port=5463;Database=suwageval_test;Username=suwageval;Password=suwageval"
    )
    {
        Database = "suwageval_cli_test",
    }.ConnectionString;

    private string? unavailable;

    // Any type of the server assembly names it for the factory (its own Program clashes with the CLI's).
    private WebApplicationFactory<Health>? root;
    private WebApplicationFactory<Health>? app;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await RecreateAsync();
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
            return;
        }
        root = new WebApplicationFactory<Health>();
        app = root.WithWebHostBuilder(host => host.UseSetting("ConnectionStrings:Default", ConnectionString));
    }

    /// <summary>A client on the in-process server — or the test is skipped (failed in CI) with the reason.</summary>
    public HttpClient Client()
    {
        if (unavailable is not null)
        {
            Assert.Skip($"PostgreSQL unavailable — start it with `just db up` ({unavailable})");
        }
        return app!.CreateClient();
    }

    private static async Task RecreateAsync()
    {
        var database = new NpgsqlConnectionStringBuilder(ConnectionString).Database;
        var admin = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
            Timeout = 3,
            Pooling = false,
        };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await create.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (root is not null)
        {
            await root.DisposeAsync();
        }
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }
}
