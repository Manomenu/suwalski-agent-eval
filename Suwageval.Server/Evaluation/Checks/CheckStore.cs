using Dapper;
using Npgsql;
using Suwageval.Server.Database;

namespace Suwageval.Server.Evaluation.Checks;

internal sealed class CheckStore(Pool pool)
{
    private const string LatestRevision = """
        SELECT c.id, c.agent_id AS AgentId, c.key, s.key AS ScenarioKey, r.revision, r.content::text AS Content,
               r.content_hash AS ContentHash, r.created_at AS CreatedAt
        FROM checks c
        JOIN scenarios s ON s.id = c.scenario_id
        JOIN LATERAL (
            SELECT * FROM check_revisions WHERE check_id = c.id ORDER BY revision DESC LIMIT 1
        ) r ON true
        WHERE c.agent_id = @agentId AND c.deleted_at IS NULL
        """;

    public Task<IReadOnlyList<CheckUpsertResult>> ReplaceAsync(
        Guid agentId,
        IReadOnlyList<CheckItem> items,
        CancellationToken cancellationToken = default
    ) => WriteAsync(agentId, items, deleteMissing: true, cancellationToken);

    public Task<IReadOnlyList<CheckUpsertResult>> UpsertAsync(
        Guid agentId,
        IReadOnlyList<CheckItem> items,
        CancellationToken cancellationToken = default
    ) => WriteAsync(agentId, items, deleteMissing: false, cancellationToken);

    private async Task<IReadOnlyList<CheckUpsertResult>> WriteAsync(
        Guid agentId,
        IReadOnlyList<CheckItem> items,
        bool deleteMissing,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var results = new List<CheckUpsertResult>();
        foreach (var item in items)
        {
            var scenarioId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT id FROM scenarios WHERE agent_id = @agentId AND key = @scenarioKey AND deleted_at IS NULL",
                    new { agentId, scenarioKey = item.ScenarioKey },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            if (scenarioId is null)
            {
                results.Add(new CheckUpsertResult(item.Key, CheckOutcome.UnknownScenario));
                continue;
            }
            var hash = ContentHash.Of(item.ContentJson);
            var existing = await connection.QuerySingleOrDefaultAsync<ExistingCheck>(
                new CommandDefinition(
                    """
                    SELECT c.id, c.deleted_at IS NOT NULL AS Deleted, c.scenario_id AS ScenarioId, r.revision, r.content_hash AS ContentHash
                    FROM checks c
                    JOIN check_revisions r ON r.check_id = c.id
                    WHERE c.agent_id = @agentId AND c.key = @key
                    ORDER BY r.revision DESC
                    LIMIT 1
                    """,
                    new { agentId, key = item.Key },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            CheckOutcome outcome;
            if (existing is null)
            {
                var id = await connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        "INSERT INTO checks (agent_id, key, scenario_id) VALUES (@agentId, @key, @scenarioId) RETURNING id",
                        new
                        {
                            agentId,
                            key = item.Key,
                            scenarioId,
                        },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                );
                await AddRevisionAsync(connection, transaction, id, 1, item.ContentJson, hash, cancellationToken);
                outcome = CheckOutcome.Created;
            }
            else if (existing.Deleted || existing.ScenarioId != scenarioId || existing.ContentHash != hash)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE checks SET scenario_id = @scenarioId, deleted_at = NULL WHERE id = @id",
                        new { scenarioId, id = existing.Id },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                );
                await AddRevisionAsync(
                    connection,
                    transaction,
                    existing.Id,
                    existing.Revision + 1,
                    item.ContentJson,
                    hash,
                    cancellationToken
                );
                outcome = CheckOutcome.Updated;
            }
            else
            {
                outcome = CheckOutcome.Unchanged;
            }
            results.Add(new CheckUpsertResult(item.Key, outcome));
        }
        if (deleteMissing && results.All(result => result.Outcome != CheckOutcome.UnknownScenario))
        {
            var deleted = await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    UPDATE checks SET deleted_at = now()
                    WHERE agent_id = @agentId AND deleted_at IS NULL AND NOT (key = ANY(@keys))
                    RETURNING key
                    """,
                    new { agentId, keys = items.Select(item => item.Key).ToArray() },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            results.AddRange(deleted.Order(StringComparer.Ordinal).Select(key => new CheckUpsertResult(key, CheckOutcome.Deleted)));
        }
        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    public async Task<IReadOnlyList<CheckRow>> ListAsync(
        Guid agentId,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CheckRow>(
            new CommandDefinition(
                $"{LatestRevision} AND (@scenarioKey::text IS NULL OR s.key = @scenarioKey) ORDER BY c.key",
                new { agentId, scenarioKey },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    public async Task<bool> DeleteAsync(Guid agentId, string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE checks SET deleted_at = now() WHERE agent_id = @agentId AND key = @key AND deleted_at IS NULL",
                new { agentId, key },
                cancellationToken: cancellationToken
            )
        );
        return deleted > 0;
    }

    private static async Task AddRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid checkId,
        int revision,
        string contentJson,
        string hash,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO check_revisions (check_id, revision, content, content_hash)
                VALUES (@checkId, @revision, @contentJson::jsonb, @hash)
                """,
                new
                {
                    checkId,
                    revision,
                    contentJson,
                    hash,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
}
