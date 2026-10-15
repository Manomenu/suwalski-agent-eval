using grate.DependencyInjection;
using grate.Migration;
using grate.postgresql.DependencyInjection;

namespace Suwageval.Server.Database;

/// <summary>
/// The schema migrations, run by grate: SQL scripts in <c>Database/Migrations/up/</c> (named
/// <c>NNNN_what_it_does.sql</c>), each run once, in name order, and recorded with a hash of its text.
/// A script is never edited after it ran somewhere: the change goes into a new file. An edited one stops
/// the migration (grate's default for one-time scripts), because databases that ran the old text and
/// databases that would run the new one would quietly differ.
/// </summary>
internal static class Migrator
{
    /// <summary>Copied next to the binary by the build (Suwageval.Server.csproj).</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Database", "Migrations");

    /// <summary>
    /// Runs the scripts that have not run yet, all in one transaction: a failing script leaves nothing behind.
    /// The database itself must exist — it comes from whoever runs PostgreSQL, never from the server.
    /// </summary>
    public static async Task MigrateAsync(string connectionString, string directory)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddSimpleConsole());
        services
            .AddGrate(grate =>
                grate
                    .WithSqlFilesDirectory(directory)
                    .WithConnectionString(connectionString)
                    // grate writes copies of what it ran here; the working directory (/app) is not writable in the image.
                    .WithOutputFolder(Path.Combine(Path.GetTempPath(), "grate"))
                    .DoNotCreateDatabase()
                    .WithTransaction()
            )
            .UsePostgreSQL();
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            await provider.GetRequiredService<IGrateMigrator>().Migrate().ConfigureAwait(false);
        }
    }
}
