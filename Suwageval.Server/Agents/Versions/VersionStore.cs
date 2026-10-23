using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents.Versions;

/// <summary>Archiving and deleting the versions of an agent. Plain SQL, mapped by Dapper.</summary>
internal sealed class VersionStore(Pool pool)
{
    /// <summary>The ids among <paramref name="versionIds"/> that are not versions of the agent.</summary>
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

    /// <summary>Archives or restores versions of the agent in one statement; an already archived version keeps its first time.</summary>
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

    /// <summary>
    /// Deletes an archived version of the agent, with its runs, samples and verdicts (the foreign keys cascade), unless one of those runs is
    /// queued or running; <paramref name="dryRun"/> only decides and counts. The version's row is locked, so no run is queued for it in between.
    /// </summary>
    public async Task<VersionDeletionResult> DeleteVersionAsync(
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
            return new VersionDeletionResult(DeletionOutcome.NotFound, null);
        }
        // The runs that go: those of the version and, however far the chain goes, the re-checks and verdict copies of them.
        var counts = await connection.QuerySingleAsync<DeletionCounts>(
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
                        WHERE s.run_id IN (SELECT id FROM doomed))::int AS Verdicts
                """,
                new { versionId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        var contents = new VersionDeletion(versionId, counts.Runs, counts.Samples, counts.Verdicts, version.Label);
        if (!version.Archived)
        {
            return new VersionDeletionResult(DeletionOutcome.NotArchived, contents);
        }
        if (counts.Active > 0)
        {
            return new VersionDeletionResult(DeletionOutcome.HasActiveRuns, contents);
        }
        if (dryRun)
        {
            return new VersionDeletionResult(DeletionOutcome.Allowed, contents);
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM agent_versions WHERE id = @versionId",
                new { versionId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return new VersionDeletionResult(DeletionOutcome.Deleted, contents);
    }

    private sealed record VersionToDelete(string? Label, bool Archived);

    private sealed record DeletionCounts(int Runs, int Active, int Samples, int Verdicts);
}
