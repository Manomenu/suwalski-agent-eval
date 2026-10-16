using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Agents;

/// <summary>Agents and their versions. Plain SQL, mapped by Dapper.</summary>
internal sealed class AgentStore(Pool pool)
{
    private const string AgentColumns = "id, name, endpoint_url AS EndpointUrl, created_at AS CreatedAt";

    private const string VersionColumns =
        "id, agent_id AS AgentId, content::text AS Content, content_hash AS ContentHash, label, created_at AS CreatedAt";

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

    public async Task<AgentCounts> CountAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<AgentCounts>(
            new CommandDefinition(
                """
                SELECT (SELECT count(*) FROM agent_versions WHERE agent_id = @agentId)::int AS Versions,
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
        CancellationToken cancellationToken = default
    )
    {
        var hash = ContentHash.Of(contentJson);
        await using var connection = await pool.OpenAsync(cancellationToken);
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
        var existing = await connection.QuerySingleAsync<AgentVersionRow>(
            new CommandDefinition(
                $"SELECT {VersionColumns} FROM agent_versions WHERE agent_id = @agentId AND content_hash = @hash",
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
