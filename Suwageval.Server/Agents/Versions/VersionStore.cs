using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents.Versions;

internal sealed class VersionStore(Pool pool)
{
    public async Task<IReadOnlyDictionary<Guid, VersionRunCounts>> RunCountsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid VersionId, int Runs, int GroupRuns)>(
            new CommandDefinition(
                $"""
                SELECT r.agent_version_id AS VersionId,
                       (COUNT(*) FILTER (WHERE {RunGroupSql.EffectiveGroup} IS NULL))::int AS Runs,
                       (COUNT(*) FILTER (WHERE {RunGroupSql.EffectiveGroup} IS NOT NULL))::int AS GroupRuns
                FROM runs r
                WHERE r.agent_id = @agentId AND r.gate_retry_limit IS NULL
                GROUP BY r.agent_version_id
                """,
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return rows.ToDictionary(row => row.VersionId, row => new VersionRunCounts(row.Runs, row.GroupRuns));
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RetrievalData>>> DataAsync(
        Guid agentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid VersionId, string DataVersion, string? Description)>(
            new CommandDefinition(
                """
                SELECT DISTINCT ON (r.agent_version_id, r.data_version)
                       r.agent_version_id AS VersionId, r.data_version AS DataVersion, r.data_description AS Description
                FROM runs r
                WHERE r.agent_id = @agentId AND r.data_version IS NOT NULL
                ORDER BY r.agent_version_id, r.data_version, (r.data_description IS NULL), r.created_at DESC
                """,
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return rows.GroupBy(row => row.VersionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RetrievalData>)[.. group.Select(row => new RetrievalData(row.DataVersion, row.Description))]
            );
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<VersionMonitoring>>> MonitoringAsync(
        Guid agentId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid VersionId, string Environment, int Conversations, DateTime LastAt, bool Current)>(
            new CommandDefinition(
                """
                WITH turns AS (
                    SELECT c.environment, t.agent_version_id, t.at
                    FROM monitoring_turns t
                    JOIN monitoring_conversations c ON c.id = t.conversation_id
                    WHERE c.agent_id = @agentId
                ),
                answered AS (
                    SELECT environment, agent_version_id, max(at) AS last_at FROM turns GROUP BY environment, agent_version_id
                ),
                current AS (
                    SELECT DISTINCT ON (environment) environment, agent_version_id FROM turns ORDER BY environment, at DESC
                ),
                conversations AS (
                    SELECT environment, agent_version_id, count(*) AS n
                    FROM monitoring_conversations
                    WHERE agent_id = @agentId AND last_turn_at >= @since
                    GROUP BY environment, agent_version_id
                )
                SELECT a.agent_version_id AS VersionId, a.environment AS Environment, COALESCE(n.n, 0)::int AS Conversations,
                       a.last_at AS LastAt, (cur.agent_version_id = a.agent_version_id) AS Current
                FROM answered a
                JOIN current cur ON cur.environment = a.environment
                LEFT JOIN conversations n ON n.environment = a.environment AND n.agent_version_id = a.agent_version_id
                ORDER BY a.environment
                """,
                new { agentId, since },
                cancellationToken: cancellationToken
            )
        );
        return rows.GroupBy(row => row.VersionId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<VersionMonitoring>)
                        [
                            .. group.Select(row => new VersionMonitoring(
                                row.Environment,
                                row.Conversations,
                                new DateTimeOffset(DateTime.SpecifyKind(row.LastAt, DateTimeKind.Utc)),
                                row.Current
                            )),
                        ]
            );
    }

    public async Task<IReadOnlyList<Guid>> UnknownVersionsAsync(
        Guid agentId,
        IReadOnlyList<Guid> versionIds,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var known = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT id FROM agent_versions WHERE agent_id = @agentId AND id = ANY(@versionIds)",
                new { agentId, versionIds = versionIds.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. versionIds.Distinct().Except(known)];
    }

    public async Task ArchiveVersionsAsync(
        Guid agentId,
        IReadOnlyList<Guid> versionIds,
        bool archived,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE agent_versions
                SET archived_at = CASE WHEN @archived THEN COALESCE(archived_at, now()) END
                WHERE agent_id = @agentId AND id = ANY(@versionIds)
                """,
                new
                {
                    agentId,
                    versionIds = versionIds.ToArray(),
                    archived,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<DeletionResult<VersionDeletion>> DeleteVersionAsync(
        Guid agentId,
        Guid versionId,
        bool dryRun,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var version = await connection.QuerySingleOrDefaultAsync<VersionToDelete>(
            new CommandDefinition(
                "SELECT label, archived_at IS NOT NULL AS Archived FROM agent_versions WHERE id = @versionId AND agent_id = @agentId FOR UPDATE",
                new { versionId, agentId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        if (version is null)
        {
            return new DeletionResult<VersionDeletion>(DeletionOutcome.NotFound, null);
        }
        var counts = await connection.QuerySingleAsync<VersionDeletionCounts>(
            new CommandDefinition(
                """
                WITH RECURSIVE doomed AS (
                    SELECT id, status FROM runs WHERE agent_version_id = @versionId
                    UNION
                    SELECT r.id, r.status FROM runs r JOIN doomed d ON r.recheck_of = d.id OR r.verdicts_from = d.id
                )
                SELECT (SELECT count(*) FROM doomed)::int AS Runs,
                       (SELECT count(*) FROM doomed WHERE status IN ('Queued', 'Running'))::int AS Active,
                       (SELECT count(*) FROM scenario_results s WHERE s.run_id IN (SELECT id FROM doomed))::int AS Samples,
                       (SELECT count(*) FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id
                        WHERE s.run_id IN (SELECT id FROM doomed))::int AS Verdicts,
                       (SELECT count(*) FROM monitoring_conversations c WHERE c.agent_version_id = @versionId
                            OR EXISTS (SELECT 1 FROM monitoring_turns t WHERE t.conversation_id = c.id AND t.agent_version_id = @versionId))::int
                           AS Conversations
                """,
                new { versionId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        var contents = new VersionDeletion(versionId, counts.Runs, counts.Samples, counts.Verdicts, version.Label, counts.Conversations);
        if (!version.Archived)
        {
            return new DeletionResult<VersionDeletion>(DeletionOutcome.NotArchived, contents);
        }
        if (counts.Active > 0)
        {
            return new DeletionResult<VersionDeletion>(DeletionOutcome.HasActiveRuns, contents);
        }
        if (dryRun)
        {
            return new DeletionResult<VersionDeletion>(DeletionOutcome.Allowed, contents);
        }
        // Why: a conversation the version answered a turn of goes whole, because the cascade alone would leave one with a gap
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM monitoring_conversations c WHERE c.agent_version_id = @versionId
                    OR EXISTS (SELECT 1 FROM monitoring_turns t WHERE t.conversation_id = c.id AND t.agent_version_id = @versionId)
                """,
                new { versionId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM agent_versions WHERE id = @versionId",
                new { versionId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return new DeletionResult<VersionDeletion>(DeletionOutcome.Deleted, contents);
    }

    private sealed record VersionToDelete(string? Label, bool Archived);

    private sealed record VersionDeletionCounts(int Runs, int Active, int Samples, int Verdicts, int Conversations);
}
