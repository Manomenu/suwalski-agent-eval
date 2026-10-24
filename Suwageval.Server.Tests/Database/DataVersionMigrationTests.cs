using Dapper;
using Npgsql;
using Suwageval.AgentSdk.Agents;
using Suwageval.Server.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Tests.Database;

/// <summary>
/// 0026_data_version_on_runs.sql on data made before it: versions that differed only in their retrieval data become one, their runs keep
/// the data they ran on, and the next registration of the same contract finds the merged version and gives it its hash.
/// </summary>
public sealed class DataVersionMigrationTests(TestDatabase database) : IAsyncLifetime
{
    private const string Migration = "0026_data_version_on_runs.sql";

    private readonly string connectionString = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString)
    {
        Database = $"data_version_probe_{Guid.NewGuid():N}",
    }.ConnectionString;
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("migrations-");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _ = database.Pool; // skips (fails in CI) without PostgreSQL
        await TestDatabase.RecreateAsync(connectionString);
        var up = Directory.CreateDirectory(Path.Combine(directory.FullName, "up"));
        foreach (var script in Directory.GetFiles(Path.Combine(Migrator.DefaultDirectory, "up")))
        {
            if (string.CompareOrdinal(Path.GetFileName(script), Migration) < 0)
            {
                File.Copy(script, Path.Combine(up.FullName, Path.GetFileName(script)));
            }
        }
        await Migrator.MigrateAsync(connectionString, directory.FullName);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await TestDatabase.DropAsync(connectionString);
        directory.Delete(recursive: true);
    }

    private static AgentContract Contract() =>
        new()
        {
            Agents =
            [
                new LlmAgentDefinition
                {
                    Name = "main",
                    Model = new ModelDefinition { Name = "some-model" },
                    Config = "Be helpful.",
                },
            ],
            Context = new ContextDefinition
            {
                UsesRetrieval = true,
                Retrieval = new RetrievalDefinition { DataVersion = "d2", Description = "1900 articles" },
            },
        };

    /// <summary>The content as the server stored it before 0026: the data version in it.</summary>
    private static string StoredBefore(string dataVersion) =>
        AgentContractHash
            .Content(Contract())
            .Replace("\"dataVersion\":null", $"\"dataVersion\":\"{dataVersion}\"", StringComparison.Ordinal);

    [Fact]
    public async Task Versions_that_differ_only_in_data_become_one_whose_runs_keep_their_data_and_the_next_registration_takes_it()
    {
        Guid agentId,
            older,
            newer,
            olderRun,
            newerRun;
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            agentId = await connection.QuerySingleAsync<Guid>(
                "INSERT INTO agents (name, endpoint_url) VALUES ('a', 'http://localhost:9/') RETURNING id"
            );
            older = await connection.QuerySingleAsync<Guid>(
                """
                INSERT INTO agent_versions (agent_id, content, content_hash, label, data_description, created_at, archived_at)
                VALUES (@agentId, @content::jsonb, 'h1', 'v1', '1832 articles', now() - interval '1 day', now())
                RETURNING id
                """,
                new { agentId, content = StoredBefore("d1") }
            );
            newer = await connection.QuerySingleAsync<Guid>(
                """
                INSERT INTO agent_versions (agent_id, content, content_hash, label, data_description)
                VALUES (@agentId, @content::jsonb, 'h2', 'v2', '1900 articles')
                RETURNING id
                """,
                new { agentId, content = StoredBefore("d2") }
            );
            olderRun = await connection.QuerySingleAsync<Guid>(
                "INSERT INTO runs (agent_id, agent_version_id, endpoint_url) VALUES (@agentId, @older, 'http://localhost:9/') RETURNING id",
                new { agentId, older }
            );
            newerRun = await connection.QuerySingleAsync<Guid>(
                "INSERT INTO runs (agent_id, agent_version_id, endpoint_url) VALUES (@agentId, @newer, 'http://localhost:9/') RETURNING id",
                new { agentId, newer }
            );
        }

        File.Copy(Path.Combine(Migrator.DefaultDirectory, "up", Migration), Path.Combine(directory.FullName, "up", Migration));
        await Migrator.MigrateAsync(connectionString, directory.FullName);

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            var versions = (
                await connection.QueryAsync<(Guid Id, string ContentHash, DateTime? ArchivedAt)>(
                    "SELECT id, content_hash, archived_at FROM agent_versions"
                )
            ).ToList();
            var kept = Assert.Single(versions);
            Assert.Equal(older, kept.Id);
            Assert.Null(kept.ArchivedAt);
            Assert.StartsWith("pending:", kept.ContentHash, StringComparison.Ordinal);
            var runs = (
                await connection.QueryAsync<(Guid Id, Guid VersionId, string DataVersion, string DataDescription)>(
                    "SELECT id, agent_version_id, data_version, data_description FROM runs ORDER BY created_at"
                )
            ).ToDictionary(run => run.Id);
            Assert.Equal(
                (older, "d1", "1832 articles"),
                (runs[olderRun].VersionId, runs[olderRun].DataVersion, runs[olderRun].DataDescription)
            );
            Assert.Equal(
                (older, "d2", "1900 articles"),
                (runs[newerRun].VersionId, runs[newerRun].DataVersion, runs[newerRun].DataDescription)
            );
        }

        await using var pool = Pool.Create(connectionString, maxSize: 2);
        var registration = await new AgentStore(pool).RegisterVersionAsync(
            agentId,
            AgentContractHash.Content(Contract()),
            "v3",
            Cancellation
        );

        Assert.False(registration.Created);
        Assert.Equal(older, registration.Version.Id);
        Assert.Equal(AgentContractHash.Of(Contract()), registration.Version.ContentHash);
    }
}
