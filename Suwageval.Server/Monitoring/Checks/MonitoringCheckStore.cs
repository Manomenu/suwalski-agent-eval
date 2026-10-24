using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Database;

namespace Suwageval.Server.Monitoring.Checks;

/// <summary>The agent's monitoring checks and their revisions. Plain SQL, mapped by Dapper.</summary>
internal sealed class MonitoringCheckStore(Pool pool)
{
    /// <summary>
    /// Makes the agent's monitoring checks the given ones, in one transaction: a new or changed check gets a revision, an unchanged one
    /// none, and a live check not in the list is deleted (its revisions and results stay). One result per given check, in order, then one
    /// per deleted check.
    /// </summary>
    public async Task<IReadOnlyList<CheckResult>> ReplaceAsync(
        Guid agentId,
        IReadOnlyList<CheckInput> checks,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var results = new List<CheckResult>();
        foreach (var check in checks)
        {
            var content = JsonSerializer.Serialize(check, ContractJson.Options);
            var hash = ContentHash.Of(content);
            var existing = await connection.QuerySingleOrDefaultAsync<(Guid Id, bool Deleted, int Revision, string ContentHash)?>(
                new CommandDefinition(
                    """
                    SELECT c.id, c.deleted_at IS NOT NULL, r.revision, r.content_hash
                    FROM monitoring_checks c JOIN monitoring_check_revisions r ON r.check_id = c.id
                    WHERE c.agent_id = @agentId AND c.key = @key
                    ORDER BY r.revision DESC LIMIT 1
                    """,
                    new { agentId, key = check.Key },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            UpsertOutcome outcome;
            if (existing is not { } known)
            {
                var id = await connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        """
                        INSERT INTO monitoring_checks (agent_id, key) VALUES (@agentId, @key)
                        ON CONFLICT (agent_id, key) DO UPDATE SET deleted_at = NULL
                        RETURNING id
                        """,
                        new { agentId, key = check.Key },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                );
                await AddRevisionAsync(connection, transaction, id, 1, content, hash, cancellationToken);
                outcome = UpsertOutcome.Created;
            }
            else if (known.Deleted || known.ContentHash != hash)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE monitoring_checks SET deleted_at = NULL WHERE id = @id",
                        new { id = known.Id },
                        transaction,
                        cancellationToken: cancellationToken
                    )
                );
                await AddRevisionAsync(connection, transaction, known.Id, known.Revision + 1, content, hash, cancellationToken);
                outcome = known.Deleted ? UpsertOutcome.Created : UpsertOutcome.Updated;
            }
            else
            {
                outcome = UpsertOutcome.Unchanged;
            }
            results.Add(new CheckResult(check.Key, outcome));
        }
        var deleted = await connection.QueryAsync<string>(
            new CommandDefinition(
                """
                UPDATE monitoring_checks SET deleted_at = now()
                WHERE agent_id = @agentId AND deleted_at IS NULL AND NOT (key = ANY(@keys))
                RETURNING key
                """,
                new { agentId, keys = checks.Select(check => check.Key).ToArray() },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        results.AddRange(deleted.Order(StringComparer.Ordinal).Select(key => new CheckResult(key, UpsertOutcome.Deleted)));
        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    /// <summary>
    /// The agent's live monitoring checks with their latest revision, by key, and their verdicts over the conversations whose last turn came
    /// in the last 7 and 30 days before <paramref name="now"/>.
    /// </summary>
    public async Task<IReadOnlyList<MonitoringCheckRow>> ListAsync(
        Guid agentId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<MonitoringCheckRow>(
                new CommandDefinition(
                    """
                    SELECT c.id, c.key, r.revision, r.content::text AS Content, r.created_at AS CreatedAt,
                           (count(*) FILTER (WHERE res.passed AND conv.last_turn_at >= @now - interval '7 days'))::int AS Passed7,
                           (count(res.check_id) FILTER (WHERE conv.last_turn_at >= @now - interval '7 days'))::int AS Checked7,
                           (count(*) FILTER (WHERE res.passed AND conv.last_turn_at >= @now - interval '30 days'))::int AS Passed30,
                           (count(res.check_id) FILTER (WHERE conv.last_turn_at >= @now - interval '30 days'))::int AS Checked30
                    FROM monitoring_checks c
                    JOIN LATERAL (
                        SELECT * FROM monitoring_check_revisions WHERE check_id = c.id ORDER BY revision DESC LIMIT 1
                    ) r ON true
                    LEFT JOIN monitoring_check_results res ON res.check_id = c.id AND NOT res.not_judged
                    LEFT JOIN monitoring_conversations conv ON conv.id = res.conversation_id
                    WHERE c.agent_id = @agentId AND c.deleted_at IS NULL
                    GROUP BY c.id, c.key, r.revision, r.content, r.created_at
                    ORDER BY c.key
                    """,
                    new { agentId, now },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    private static Task<int> AddRevisionAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid checkId,
        int revision,
        string content,
        string hash,
        CancellationToken cancellationToken
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO monitoring_check_revisions (check_id, revision, content, content_hash)
                VALUES (@checkId, @revision, @content::jsonb, @hash)
                """,
                new
                {
                    checkId,
                    revision,
                    content,
                    hash,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
}
