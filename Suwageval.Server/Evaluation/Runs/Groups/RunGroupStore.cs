using Dapper;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Evaluation.Runs.Groups;

/// <summary>The groups of an agent's runs: listing, archiving and deleting them. Plain SQL, mapped by Dapper.</summary>
internal sealed class RunGroupStore(Pool pool)
{
    /// <summary>The groups of an agent's runs, archived ones too, the one with the newest run first.</summary>
    public async Task<IReadOnlyList<RunGroupRow>> ListAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RunGroupRow>(
            new CommandDefinition(
                """
                SELECT r.run_group AS Name, count(*)::int AS Runs, count(DISTINCT r.agent_version_id)::int AS Versions,
                       max(r.created_at) AS NewestRunAt, g.archived_at AS ArchivedAt
                FROM runs r
                LEFT JOIN archived_run_groups g ON g.agent_id = r.agent_id AND g.name = r.run_group
                WHERE r.agent_id = @agentId AND r.run_group IS NOT NULL
                GROUP BY r.run_group, g.archived_at
                ORDER BY max(r.created_at) DESC, r.run_group
                """,
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>Sets the note of a group of the agent (null deletes it); false when no run of the agent was started in the group.</summary>
    public async Task<bool> SetNoteAsync(Guid agentId, string name, string? note, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (
            !await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    "SELECT EXISTS (SELECT 1 FROM runs WHERE agent_id = @agentId AND run_group = @name)",
                    new { agentId, name },
                    cancellationToken: cancellationToken
                )
            )
        )
        {
            return false;
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                note is null
                    ? "DELETE FROM run_group_notes WHERE agent_id = @agentId AND name = @name"
                    : """
                    INSERT INTO run_group_notes (agent_id, name, note) VALUES (@agentId, @name, @note)
                    ON CONFLICT (agent_id, name) DO UPDATE SET note = EXCLUDED.note
                    """,
                new
                {
                    agentId,
                    name,
                    note,
                },
                cancellationToken: cancellationToken
            )
        );
        return true;
    }

    /// <summary>The names among <paramref name="names"/> that no run of the agent was started in.</summary>
    public async Task<IReadOnlyList<string>> UnknownAsync(
        Guid agentId,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var known = await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT DISTINCT run_group FROM runs WHERE agent_id = @agentId AND run_group = ANY(@names)",
                new { agentId, names = names.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. names.Distinct().Except(known)];
    }

    /// <summary>Archives or restores groups of the agent in one statement; an already archived group keeps its first time.</summary>
    public async Task ArchiveAsync(Guid agentId, IReadOnlyList<string> names, bool archived, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                archived
                    ? """
                    INSERT INTO archived_run_groups (agent_id, name)
                    SELECT @agentId, name FROM unnest(@names::text[]) AS name
                    ON CONFLICT DO NOTHING
                    """
                    : "DELETE FROM archived_run_groups WHERE agent_id = @agentId AND name = ANY(@names)",
                new { agentId, names = names.ToArray() },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>
    /// Deletes an archived group of the agent with its runs, samples and verdicts (the foreign keys cascade), unless one of those runs is queued
    /// or running; <paramref name="dryRun"/> only decides and counts. The group's archive row is locked, so no run is queued in it in between
    /// (starting one would put the group back in use).
    /// </summary>
    public async Task<DeletionResult<RunGroupDeletion>> DeleteAsync(
        Guid agentId,
        string name,
        bool dryRun,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var archived = await connection.QuerySingleOrDefaultAsync<DateTime?>(
            new CommandDefinition(
                "SELECT archived_at FROM archived_run_groups WHERE agent_id = @agentId AND name = @name FOR UPDATE",
                new { agentId, name },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        // The runs that go: those started in the group and, however far the chain goes, the re-checks and verdict copies of them.
        var counts = await connection.QuerySingleAsync<DeletionCounts>(
            new CommandDefinition(
                """
                WITH RECURSIVE doomed AS (
                    SELECT id, status FROM runs WHERE agent_id = @agentId AND run_group = @name
                    UNION
                    SELECT r.id, r.status FROM runs r JOIN doomed d ON r.recheck_of = d.id OR r.verdicts_from = d.id
                )
                SELECT (SELECT count(*) FROM doomed)::int AS Runs,
                       (SELECT count(*) FROM doomed WHERE status IN ('Queued', 'Running'))::int AS Active,
                       (SELECT count(*) FROM scenario_results s WHERE s.run_id IN (SELECT id FROM doomed))::int AS Samples,
                       (SELECT count(*) FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id
                        WHERE s.run_id IN (SELECT id FROM doomed))::int AS Verdicts
                """,
                new { agentId, name },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        if (counts.Runs == 0)
        {
            return new DeletionResult<RunGroupDeletion>(DeletionOutcome.NotFound, null);
        }
        var contents = new RunGroupDeletion(name, counts.Runs, counts.Samples, counts.Verdicts);
        if (archived is null)
        {
            return new DeletionResult<RunGroupDeletion>(DeletionOutcome.NotArchived, contents);
        }
        if (counts.Active > 0)
        {
            return new DeletionResult<RunGroupDeletion>(DeletionOutcome.HasActiveRuns, contents);
        }
        if (dryRun)
        {
            return new DeletionResult<RunGroupDeletion>(DeletionOutcome.Allowed, contents);
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                WITH RECURSIVE doomed AS (
                    SELECT id FROM runs WHERE agent_id = @agentId AND run_group = @name
                    UNION
                    SELECT r.id FROM runs r JOIN doomed d ON r.recheck_of = d.id OR r.verdicts_from = d.id
                )
                DELETE FROM runs WHERE id IN (SELECT id FROM doomed)
                """,
                new { agentId, name },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM archived_run_groups WHERE agent_id = @agentId AND name = @name",
                new { agentId, name },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM run_group_notes WHERE agent_id = @agentId AND name = @name",
                new { agentId, name },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return new DeletionResult<RunGroupDeletion>(DeletionOutcome.Deleted, contents);
    }

    private sealed record DeletionCounts(int Runs, int Active, int Samples, int Verdicts);
}
