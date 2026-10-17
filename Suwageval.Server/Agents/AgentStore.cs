using Dapper;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents;

/// <summary>Agents and their versions. Plain SQL, mapped by Dapper.</summary>
internal sealed class AgentStore(Pool pool)
{
    private const string AgentColumns =
        "id, name, endpoint_url AS EndpointUrl, created_at AS CreatedAt, to_json(regression_branches)::text AS RegressionBranches, regression_history AS RegressionHistory, samples";

    private const string VersionColumns =
        "id, agent_id AS AgentId, content::text AS Content, content_hash AS ContentHash, label, data_description AS DataDescription, created_at AS CreatedAt, archived_at AS ArchivedAt";

    public async Task<AgentCreation> CreateAsync(string name, string endpointUrl, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var created = await connection.QuerySingleOrDefaultAsync<AgentRow>(
            new CommandDefinition(
                $"INSERT INTO agents (name, endpoint_url) VALUES (@name, @endpointUrl) ON CONFLICT (name) DO NOTHING RETURNING {AgentColumns}",
                new { name, endpointUrl },
                cancellationToken: cancellationToken
            )
        );
        if (created is not null)
        {
            return new AgentCreation(created, Created: true);
        }
        var existing = await connection.QuerySingleAsync<AgentRow>(
            new CommandDefinition(
                $"SELECT {AgentColumns} FROM agents WHERE name = @name",
                new { name },
                cancellationToken: cancellationToken
            )
        );
        return new AgentCreation(existing, Created: false);
    }

    public async Task<AgentRow> SetEndpointUrlAsync(Guid id, string endpointUrl, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<AgentRow>(
            new CommandDefinition(
                $"UPDATE agents SET endpoint_url = @endpointUrl WHERE id = @id RETURNING {AgentColumns}",
                new { id, endpointUrl },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>Stores the agent's default samples and regression settings; none puts the defaults back.</summary>
    public async Task<AgentRow> SetSettingsAsync(
        Guid id,
        int? samples,
        RegressionSettings? settings,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<AgentRow>(
            new CommandDefinition(
                $"UPDATE agents SET samples = @samples, regression_branches = @branches, regression_history = @history WHERE id = @id RETURNING {AgentColumns}",
                new
                {
                    id,
                    samples,
                    branches = settings?.Branches?.ToArray(),
                    history = settings?.History,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<AgentCounts> CountAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<AgentCounts>(
            new CommandDefinition(
                """
                SELECT (SELECT count(*) FROM agent_versions WHERE agent_id = @agentId AND archived_at IS NULL)::int AS Versions,
                       (SELECT count(*) FROM scenarios WHERE agent_id = @agentId AND deleted_at IS NULL)::int AS Scenarios,
                       (SELECT count(*) FROM checks WHERE agent_id = @agentId AND deleted_at IS NULL)::int AS Checks
                """,
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>The agent by its name or its id.</summary>
    public async Task<AgentRow?> GetAsync(string nameOrId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AgentRow>(
            new CommandDefinition(
                $"SELECT {AgentColumns} FROM agents WHERE name = @nameOrId OR id::text = @nameOrId",
                new { nameOrId },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<IReadOnlyList<AgentRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AgentRow>(
            new CommandDefinition($"SELECT {AgentColumns} FROM agents ORDER BY name", cancellationToken: cancellationToken)
        );
        return [.. rows];
    }

    /// <summary>Registers a version; content seen before (same hash) gives the existing version back.</summary>
    public async Task<AgentVersionRegistration> RegisterVersionAsync(
        Guid agentId,
        string contentJson,
        string? label,
        string? dataDescription = null,
        CancellationToken cancellationToken = default
    )
    {
        var hash = ContentHash.Of(contentJson);
        await using var connection = await pool.OpenAsync(cancellationToken);
        var created = await connection.QuerySingleOrDefaultAsync<AgentVersionRow>(
            new CommandDefinition(
                $"""
                INSERT INTO agent_versions (agent_id, content, content_hash, label, data_description)
                VALUES (@agentId, @contentJson::jsonb, @hash, @label, @dataDescription)
                ON CONFLICT (agent_id, content_hash) DO NOTHING
                RETURNING {VersionColumns}
                """,
                new
                {
                    agentId,
                    contentJson,
                    hash,
                    label,
                    dataDescription,
                },
                cancellationToken: cancellationToken
            )
        );
        if (created is not null)
        {
            return new AgentVersionRegistration(created, Created: true);
        }
        // An archived version the agent serves again is back in use.
        var existing = await connection.QuerySingleAsync<AgentVersionRow>(
            new CommandDefinition(
                $"UPDATE agent_versions SET archived_at = NULL WHERE agent_id = @agentId AND content_hash = @hash RETURNING {VersionColumns}",
                new { agentId, hash },
                cancellationToken: cancellationToken
            )
        );
        return new AgentVersionRegistration(existing, Created: false);
    }

    /// <summary>Newest first.</summary>
    public async Task<IReadOnlyList<AgentVersionRow>> ListVersionsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AgentVersionRow>(
            new CommandDefinition(
                $"SELECT {VersionColumns} FROM agent_versions WHERE agent_id = @agentId ORDER BY created_at DESC, id",
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

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

    /// <summary>
    /// Deletes the agent with everything that belongs to it (the foreign keys cascade), unless one of its runs is queued or running;
    /// <paramref name="dryRun"/> only decides and counts. The agent's row is locked, so no run is queued for it in between.
    /// </summary>
    public async Task<AgentDeletionResult> DeleteAgentAsync(Guid agentId, bool dryRun, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var name = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT name FROM agents WHERE id = @agentId FOR UPDATE",
                new { agentId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        if (name is null)
        {
            return new AgentDeletionResult(DeletionOutcome.NotFound, null);
        }
        var counts = await connection.QuerySingleAsync<AgentDeletionCounts>(
            new CommandDefinition(
                """
                SELECT (SELECT count(*) FROM agent_versions WHERE agent_id = @agentId)::int AS Versions,
                       (SELECT count(*) FROM scenarios WHERE agent_id = @agentId)::int AS Scenarios,
                       (SELECT count(*) FROM checks WHERE agent_id = @agentId)::int AS Checks,
                       (SELECT count(*) FROM runs WHERE agent_id = @agentId)::int AS Runs,
                       (SELECT count(*) FROM runs WHERE agent_id = @agentId AND status IN ('Queued', 'Running'))::int AS Active,
                       (SELECT count(*) FROM scenario_results s JOIN runs r ON r.id = s.run_id WHERE r.agent_id = @agentId)::int AS Samples,
                       (SELECT count(*) FROM check_results c
                        JOIN scenario_results s ON s.id = c.scenario_result_id
                        JOIN runs r ON r.id = s.run_id WHERE r.agent_id = @agentId)::int AS Verdicts
                """,
                new { agentId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        var contents = new AgentDeletion(
            agentId,
            name,
            counts.Versions,
            counts.Scenarios,
            counts.Checks,
            counts.Runs,
            counts.Samples,
            counts.Verdicts
        );
        if (counts.Active > 0)
        {
            return new AgentDeletionResult(DeletionOutcome.HasActiveRuns, contents);
        }
        if (dryRun)
        {
            return new AgentDeletionResult(DeletionOutcome.Allowed, contents);
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM agents WHERE id = @agentId",
                new { agentId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return new AgentDeletionResult(DeletionOutcome.Deleted, contents);
    }

    private sealed record AgentDeletionCounts(int Versions, int Scenarios, int Checks, int Runs, int Active, int Samples, int Verdicts);

    private sealed record VersionToDelete(string? Label, bool Archived);

    private sealed record DeletionCounts(int Runs, int Active, int Samples, int Verdicts);

    public async Task<AgentVersionRow?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AgentVersionRow>(
            new CommandDefinition(
                $"SELECT {VersionColumns} FROM agent_versions WHERE id = @versionId",
                new { versionId },
                cancellationToken: cancellationToken
            )
        );
    }
}
