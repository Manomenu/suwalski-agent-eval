using AgentEval.Server.Db;
using Npgsql;

namespace AgentEval.Server.Tests.Db;

/// <summary>Each test migrates into a schema of its own, dropped afterwards, next to the real schema.</summary>
public sealed class MigrationsTests(TestDatabase database) : IAsyncLifetime
{
    private readonly string schema = $"migration_probe_{Guid.NewGuid():N}";
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("migrations-");
    private NpgsqlConnection connection = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        connection = await database.Pool.OpenAsync();
        await ExecuteAsync($"CREATE SCHEMA {schema}; SET search_path TO {schema}");
    }

    public async ValueTask DisposeAsync()
    {
        await ExecuteAsync($"RESET search_path; DROP SCHEMA {schema} CASCADE");
        await connection.DisposeAsync();
        directory.Delete(recursive: true);
    }

    [Fact]
    public async Task New_migrations_run_once_and_are_recorded()
    {
        var first = Write("001_a.sql", "CREATE TABLE a (x int);");

        Assert.Equal(["001_a.sql"], await Migrations.MigrateAsync(connection, directory.FullName, Cancellation));
        Assert.Empty(await Migrations.MigrateAsync(connection, directory.FullName, Cancellation));
        Assert.Equal([Migrations.Checksum(first)], await QueryAsync("SELECT checksum FROM schema_migrations"));
    }

    [Fact]
    public async Task An_edited_migration_stops_the_server()
    {
        Write("001_a.sql", "CREATE TABLE a (x int);");
        await Migrations.MigrateAsync(connection, directory.FullName, Cancellation);
        Write("001_a.sql", "CREATE TABLE a (x int, y int);");

        var error = await Assert.ThrowsAsync<MigrationChangedException>(() =>
            Migrations.MigrateAsync(connection, directory.FullName, Cancellation)
        );

        Assert.StartsWith("migration 001_a.sql was edited", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_deleted_migration_stops_the_server()
    {
        var file = Write("001_a.sql", "CREATE TABLE a (x int);");
        await Migrations.MigrateAsync(connection, directory.FullName, Cancellation);
        File.Delete(file);

        var error = await Assert.ThrowsAsync<MigrationChangedException>(() =>
            Migrations.MigrateAsync(connection, directory.FullName, Cancellation)
        );

        Assert.Equal("migration 001_a.sql ran on this database, but its file is gone", error.Message);
    }

    [Fact]
    public async Task A_failing_migration_leaves_nothing_behind()
    {
        Write("001_a.sql", "CREATE TABLE a (x int);");
        Write("002_broken.sql", "CREATE TABLE b (x nonexistent_type);");

        await Assert.ThrowsAsync<PostgresException>(() => Migrations.MigrateAsync(connection, directory.FullName, Cancellation));

        Assert.Equal(["0"], await QueryAsync($"SELECT count(*) FROM pg_tables WHERE schemaname = '{schema}'"));
    }

    private string Write(string name, string sql)
    {
        var file = Path.Combine(directory.FullName, name);
        File.WriteAllText(file, sql);
        return file;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancellation);
        var rows = new List<string>();
        while (await reader.ReadAsync(Cancellation))
        {
            rows.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }
        return rows;
    }
}
