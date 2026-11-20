using grate.DependencyInjection;
using grate.Migration;
using grate.postgresql.DependencyInjection;

namespace Suwageval.Server.Database;

internal static class Migrator
{
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Database", "Migrations");

    public static async Task<IReadOnlyList<string>> MigrateAsync(string connectionString, string directory)
    {
        var output = Directory.CreateTempSubdirectory("suwageval-grate-");
        using var temporaryFolders = new GrateTemporaryFolders(directory);
        IReadOnlyList<string> deleted;
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddSimpleConsole().AddProvider(temporaryFolders));
            services
                .AddGrate(grate =>
                    grate
                        .WithSqlFilesDirectory(directory)
                        .WithConnectionString(connectionString)
                        .WithOutputFolder(output.FullName)
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
        finally
        {
            deleted = temporaryFolders.Delete();
            output.Delete(recursive: true);
        }
        return deleted;
    }
}
