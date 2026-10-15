using System.Security.Cryptography;
using Dapper;
using Npgsql;

namespace AgentEval.Server.Db;

/// <summary>
/// The schema migrations: numbered SQL files in <c>Db/migrations/</c>, applied in name order. Each one runs
/// once and is recorded in <c>schema_migrations</c> with a checksum; the server applies the missing ones when
/// it starts. A migration is never edited after it ran somewhere: the change goes into a new file. An edited
/// or deleted one stops the server, because databases that ran the old text and databases that would run
/// the new one would quietly differ.
/// </summary>
internal static class Migrations
{
    /// <summary>Copied next to the binary by the build (AgentEval.Server.csproj).</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Db", "migrations");

    // Any constant will do; it only has to be the same in every process that migrates.
    private const long MigrationLock = 0x6167_6576; // "agev"

    private sealed record AppliedMigration(string Name, string Checksum);

    public static string Checksum(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    /// <summary>
    /// Checks the applied migrations against their files, applies the new ones and returns their names.
    /// Everything runs in one transaction under an advisory lock, so two server processes starting at once
    /// neither apply a migration twice nor see one half applied.
    /// </summary>
    public static async Task<IReadOnlyList<string>> MigrateAsync(
        NpgsqlConnection connection,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        var files = Directory
            .GetFiles(directory, "*.sql")
            .Order(StringComparer.Ordinal)
            .ToDictionary(file => Path.GetFileName(file), StringComparer.Ordinal);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync("SELECT pg_advisory_xact_lock(@key)", new { key = MigrationLock });
        await ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, checksum text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())"
        );
        var rows = await connection.QueryAsync<AppliedMigration>(
            new CommandDefinition(
                "SELECT name, checksum FROM schema_migrations",
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );
        var applied = rows.ToDictionary(row => row.Name, row => row.Checksum, StringComparer.Ordinal);

        foreach (var (name, recorded) in applied)
        {
            if (!files.TryGetValue(name, out var file))
            {
                throw new MigrationChangedException($"migration {name} ran on this database, but its file is gone");
            }
            if (recorded != Checksum(file))
            {
                throw new MigrationChangedException(
                    $"migration {name} was edited after it ran on this database; put the change in a new migration"
                );
            }
        }

        var added = files.Where(entry => !applied.ContainsKey(entry.Key)).ToList();
        foreach (var (name, file) in added)
        {
            await ExecuteAsync(await File.ReadAllTextAsync(file, cancellationToken));
            await ExecuteAsync(
                "INSERT INTO schema_migrations (name, checksum) VALUES (@name, @checksum)",
                new { name, checksum = Checksum(file) }
            );
        }
        await transaction.CommitAsync(cancellationToken);
        return [.. added.Select(entry => entry.Key)];

        Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            return connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        }
    }
}
