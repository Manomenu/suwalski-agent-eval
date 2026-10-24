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
        "id, agent_id AS AgentId, content::text AS Content, content_hash AS ContentHash, label, created_at AS CreatedAt, archived_at AS ArchivedAt";

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

    /// <summary>
    /// Registers a version; content seen before (same hash) gives the existing version back. A version whose hash a migration could
    /// not compute (<c>pending:</c>, 0026) is found by its content and takes the hash now.
    /// </summary>
    public async Task<AgentVersionRegistration> RegisterVersionAsync(
        Guid agentId,
        string contentJson,
        string? label,
        CancellationToken cancellationToken = default
    )
    {
        var hash = ContentHash.Of(contentJson);
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE agent_versions SET content_hash = @hash
                WHERE agent_id = @agentId AND content_hash LIKE 'pending:%' AND content = @contentJson::jsonb
                    AND NOT EXISTS (SELECT 1 FROM agent_versions AS known WHERE known.agent_id = @agentId AND known.content_hash = @hash)
                """,
                new
                {
                    agentId,
                    contentJson,
                    hash,
                },
                cancellationToken: cancellationToken
            )
        );
        var created = await connection.QuerySingleOrDefaultAsync<AgentVersionRow>(
            new CommandDefinition(
                $"""
                INSERT INTO agent_versions (agent_id, content, content_hash, label)
                VALUES (@agentId, @contentJson::jsonb, @hash, @label)
                ON CONFLICT (agent_id, content_hash) DO NOTHING
                RETURNING {VersionColumns}
                """,
                new
                {
                    agentId,
                    contentJson,
                    hash,
                    label,
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

    /// <summary>
    /// Deletes the agent with everything that belongs to it (the foreign keys cascade), unless one of its runs is queued or running;
    /// <paramref name="dryRun"/> only decides and counts. The agent's row is locked, so no run is queued for it in between.
    /// </summary>
    public async Task<DeletionResult<AgentDeletion>> DeleteAgentAsync(
        Guid agentId,
        bool dryRun,
        CancellationToken cancellationToken = default
    )
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
            return new DeletionResult<AgentDeletion>(DeletionOutcome.NotFound, null);
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
            return new DeletionResult<AgentDeletion>(DeletionOutcome.HasActiveRuns, contents);
        }
        if (dryRun)
        {
            return new DeletionResult<AgentDeletion>(DeletionOutcome.Allowed, contents);
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
        return new DeletionResult<AgentDeletion>(DeletionOutcome.Deleted, contents);
    }

    private sealed record AgentDeletionCounts(int Versions, int Scenarios, int Checks, int Runs, int Active, int Samples, int Verdicts);

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
