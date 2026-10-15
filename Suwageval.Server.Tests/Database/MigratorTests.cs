using Npgsql;
using Suwageval.Server.Database;

namespace Suwageval.Server.Tests.Database;

/// <summary>Each test migrates into a scratch database of its own, dropped afterwards.</summary>
public sealed class MigratorTests(TestDatabase database) : IAsyncLifetime
{
    private readonly string connectionString = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString)
    {
        Database = $"migrator_probe_{Guid.NewGuid():N}",
    }.ConnectionString;
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("migrations-");

    public async ValueTask InitializeAsync()
    {
        _ = database.Pool; // skips (fails in CI) without PostgreSQL
        await TestDatabase.RecreateAsync(connectionString);
        Directory.CreateDirectory(Path.Combine(directory.FullName, "up"));
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await TestDatabase.DropAsync(connectionString);
        directory.Delete(recursive: true);
    }

    [Fact]
    public async Task A_new_script_runs_once_and_only_once()
    {
        Write("0001_counter.sql", "CREATE TABLE counter (x int); INSERT INTO counter VALUES (1);");

        await Migrator.MigrateAsync(connectionString, directory.FullName);
        await Migrator.MigrateAsync(connectionString, directory.FullName);

        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM counter"));
    }

    [Fact]
    public async Task A_script_edited_after_it_ran_fails_the_migration()
    {
        Write("0001_counter.sql", "CREATE TABLE IF NOT EXISTS counter (x int);");
        await Migrator.MigrateAsync(connectionString, directory.FullName);
        Write("0001_counter.sql", "CREATE TABLE IF NOT EXISTS counter (x int, y int);");

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Migrator.MigrateAsync(connectionString, directory.FullName));

        Assert.Contains("0001_counter.sql", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_script_leaves_nothing_behind()
    {
        Write("0001_fine.sql", "CREATE TABLE fine (x int);");
        Write("0002_broken.sql", "CREATE TABLE broken (x int); SELECT * FROM does_not_exist;");

        await Assert.ThrowsAnyAsync<Exception>(() => Migrator.MigrateAsync(connectionString, directory.FullName));

        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM pg_tables WHERE tablename IN ('fine', 'broken')"));
    }

    private void Write(string name, string sql) => File.WriteAllText(Path.Combine(directory.FullName, "up", name), sql);

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
