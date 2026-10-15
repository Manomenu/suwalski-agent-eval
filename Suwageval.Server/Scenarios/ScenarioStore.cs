using Dapper;
using Npgsql;
using Suwageval.Server.Database;

namespace Suwageval.Server.Scenarios;

/// <summary>Scenarios, their revisions and their tags. Plain SQL, mapped by Dapper.</summary>
internal sealed class ScenarioStore(Pool pool)
{
    private const string LatestRevision = """
        SELECT s.id, s.agent_id AS AgentId, s.key, s.tags, r.revision, r.content::text AS Content,
               r.content_hash AS ContentHash, r.created_at AS CreatedAt
        FROM scenarios s
        JOIN LATERAL (
            SELECT * FROM scenario_revisions WHERE scenario_id = s.id ORDER BY revision DESC LIMIT 1
        ) r ON true
        WHERE s.agent_id = @agentId AND s.deleted_at IS NULL
        """;

    /// <summary>
    /// Writes the scenarios in one transaction; keys not in the list are untouched. A new revision exists only
    /// when the content changed (or the scenario comes back from deletion); tags alone change in place and the
    /// scenario then counts as <see cref="ScenarioOutcome.Unchanged"/>.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioUpsertResult>> UpsertAsync(
        Guid agentId,
        IReadOnlyList<ScenarioItem> items,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var results = new List<ScenarioUpsertResult>();
        foreach (var item in items)
        {
            var tags = item.Tags.Distinct().Order().ToArray();
            var hash = ContentHash.Of(item.ContentJson);
            var existing = await connection.QuerySingleOrDefaultAsync<ExistingScenario>(
                new CommandDefinition(
                    """
                    SELECT s.id, s.deleted_at IS NOT NULL AS Deleted, s.tags, r.revision, r.content_hash AS ContentHash
                    FROM scenarios s
                    JOIN scenario_revisions r ON r.scenario_id = s.id
                    WHERE s.agent_id = @agentId AND s.key = @key
                    ORDER BY r.revision DESC
                    LIMIT 1
                    """,
                    new { agentId, key = item.Key },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            ScenarioOutcome outcome;
            if (existing is null)
            {
                var id = await connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        "INSERT INTO scenarios (agent_id, key, tags) VALUES (@agentId, @key, @tags) RETURNING id",
                        new
                        {
                            agentId,
                            key = item.Key,
                            tags,
                        },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                );
                await AddRevisionAsync(connection, transaction, id, 1, item.ContentJson, hash, cancellationToken);
                outcome = ScenarioOutcome.Created;
            }
            else if (existing.Deleted || existing.ContentHash != hash)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE scenarios SET tags = @tags, deleted_at = NULL WHERE id = @id",
                        new { tags, id = existing.Id },
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
                outcome = ScenarioOutcome.Updated;
            }
            else
            {
                if (!existing.Tags.SequenceEqual(tags))
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            "UPDATE scenarios SET tags = @tags WHERE id = @id",
                            new { tags, id = existing.Id },
                            transaction,
                            cancellationToken: cancellationToken
                        )
                    );
                }
                outcome = ScenarioOutcome.Unchanged;
            }
            results.Add(new ScenarioUpsertResult(item.Key, outcome));
        }
        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    /// <summary>The live scenarios with their latest revision, optionally only those with <paramref name="tag"/>.</summary>
    public async Task<IReadOnlyList<ScenarioRow>> ListAsync(Guid agentId, string? tag = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ScenarioRow>(
            new CommandDefinition(
                $"{LatestRevision} AND (@tag::text IS NULL OR s.tags @> ARRAY[@tag::text]) ORDER BY s.key",
                new { agentId, tag },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    public async Task<ScenarioRow?> GetAsync(Guid agentId, string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ScenarioRow>(
            new CommandDefinition($"{LatestRevision} AND s.key = @key", new { agentId, key }, cancellationToken: cancellationToken)
        );
    }

    /// <summary>Every revision of the scenario, oldest first — deleted or not.</summary>
    public async Task<IReadOnlyList<ScenarioRevisionRow>> ListRevisionsAsync(
        Guid agentId,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ScenarioRevisionRow>(
            new CommandDefinition(
                """
                SELECT r.revision, r.content::text AS Content, r.content_hash AS ContentHash, r.created_at AS CreatedAt
                FROM scenarios s
                JOIN scenario_revisions r ON r.scenario_id = s.id
                WHERE s.agent_id = @agentId AND s.key = @key
                ORDER BY r.revision
                """,
                new { agentId, key },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>Soft delete: the scenario disappears from lists, its revisions stay. False when there was nothing to delete.</summary>
    public async Task<bool> DeleteAsync(Guid agentId, string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE scenarios SET deleted_at = now() WHERE agent_id = @agentId AND key = @key AND deleted_at IS NULL",
                new { agentId, key },
                cancellationToken: cancellationToken
            )
        );
        return deleted > 0;
    }

    /// <summary>Adds and removes tags on live scenarios, with no new revision. Keys that match no live scenario are reported.</summary>
    public async Task<TagChange> ChangeTagsAsync(
        Guid agentId,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> add,
        IReadOnlyList<string> remove,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var changed = await connection.QueryAsync<ScenarioTags>(
            new CommandDefinition(
                """
                UPDATE scenarios
                SET tags = ARRAY(
                    SELECT DISTINCT tag FROM unnest(tags || @add) AS tag WHERE tag <> ALL (@remove) ORDER BY tag
                )
                WHERE agent_id = @agentId AND key = ANY (@keys) AND deleted_at IS NULL
                RETURNING key, tags
                """,
                new
                {
                    agentId,
                    keys = keys.ToArray(),
                    add = add.ToArray(),
                    remove = remove.ToArray(),
                },
                cancellationToken: cancellationToken
            )
        );
        var rows = changed.OrderBy(row => row.Key, StringComparer.Ordinal).ToList();
        var known = rows.Select(row => row.Key).ToHashSet();
        return new TagChange(rows, [.. keys.Distinct().Where(key => !known.Contains(key))]);
    }

    /// <summary>Every tag in use on live scenarios, with how many scenarios carry it.</summary>
    public async Task<IReadOnlyList<TagCount>> ListTagsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<TagCount>(
            new CommandDefinition(
                """
                SELECT tag, count(*)::int AS ScenarioCount
                FROM scenarios, unnest(tags) AS tag
                WHERE agent_id = @agentId AND deleted_at IS NULL
                GROUP BY tag
                ORDER BY tag
                """,
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    private static async Task AddRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid scenarioId,
        int revision,
        string contentJson,
        string hash,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO scenario_revisions (scenario_id, revision, content, content_hash)
                VALUES (@scenarioId, @revision, @contentJson::jsonb, @hash)
                """,
                new
                {
                    scenarioId,
                    revision,
                    contentJson,
                    hash,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
}
