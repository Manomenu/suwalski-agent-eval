using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Server.Database;

namespace Suwageval.Server.Monitoring.Conversations;

/// <summary>The conversations of monitored agents and their turns. Plain SQL, mapped by Dapper.</summary>
internal sealed class ConversationStore(Pool pool)
{
    private const string ConversationSelect = """
        SELECT c.id, a.name AS AgentName, c.environment, c.conversation_key AS ConversationKey, c.agent_version_id AS AgentVersionId,
               v.label AS VersionLabel, c.version_changed AS VersionChanged, c.data_version AS DataVersion, c.state, c.turns,
               c.started_at AS StartedAt, c.last_turn_at AS LastTurnAt, c.closed_at AS ClosedAt
        FROM monitoring_conversations c
        JOIN agents a ON a.id = c.agent_id
        JOIN agent_versions v ON v.id = c.agent_version_id
        """;

    private const string ListFilter = """
        WHERE c.agent_id = @agentId
          AND (@environment::text IS NULL OR c.environment = @environment)
          AND (cardinality(@versionIds::uuid[]) = 0 OR c.agent_version_id = ANY(@versionIds::uuid[]))
          AND (@state::text IS NULL OR c.state = @state)
        """;

    /// <summary>
    /// Stores the turns, answered by <paramref name="versionId"/>, in their conversations (made when new, opened again when closed), oldest
    /// first; a turn whose id is known already is skipped. Returns how many turns were new. One transaction: all or nothing.
    /// </summary>
    public async Task<int> AddTurnsAsync(
        Guid agentId,
        string environment,
        Guid versionId,
        IReadOnlyList<MonitoredTurn> turns,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var known = (
            await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    "SELECT id FROM monitoring_turns WHERE id = ANY(@ids)",
                    new { ids = turns.Select(turn => turn.Id).ToArray() },
                    transaction,
                    cancellationToken: cancellationToken
                )
            )
        ).ToHashSet();
        var stored = 0;
        foreach (var turn in turns.Where(turn => !known.Contains(turn.Id)).DistinctBy(turn => turn.Id).OrderBy(turn => turn.At))
        {
            var conversationId = await connection.QuerySingleAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO monitoring_conversations
                        (agent_id, environment, conversation_key, agent_version_id, data_version, started_at, last_turn_at, turns)
                    VALUES (@agentId, @environment, @conversationKey, @versionId, @dataVersion, @at, @at, 1)
                    ON CONFLICT (agent_id, environment, conversation_key) DO UPDATE SET
                        version_changed = monitoring_conversations.version_changed
                                          OR monitoring_conversations.agent_version_id <> EXCLUDED.agent_version_id,
                        agent_version_id = EXCLUDED.agent_version_id,
                        data_version = EXCLUDED.data_version,
                        state = 'Open',
                        closed_at = NULL,
                        turns = monitoring_conversations.turns + 1,
                        started_at = LEAST(monitoring_conversations.started_at, EXCLUDED.started_at),
                        last_turn_at = GREATEST(monitoring_conversations.last_turn_at, EXCLUDED.last_turn_at),
                        received_at = now()
                    RETURNING id
                    """,
                    new
                    {
                        agentId,
                        environment,
                        conversationKey = turn.ConversationId,
                        versionId,
                        dataVersion = turn.DataVersion,
                        at = turn.At,
                    },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO monitoring_turns (id, conversation_id, agent_version_id, data_version, at, items)
                    VALUES (@id, @conversationId, @versionId, @dataVersion, @at, @items::jsonb)
                    """,
                    new
                    {
                        id = turn.Id,
                        conversationId,
                        versionId,
                        dataVersion = turn.DataVersion,
                        at = turn.At,
                        items = JsonSerializer.Serialize(turn.Items, ContractJson.Options),
                    },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
            stored++;
        }
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    /// <summary>Closes the agent's open conversations whose last turn arrived before <paramref name="idleSince"/>; how many it closed.</summary>
    public async Task<int> CloseIdleAsync(Guid agentId, DateTimeOffset idleSince, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE monitoring_conversations SET state = 'Closed', closed_at = now()
                WHERE agent_id = @agentId AND state = 'Open' AND received_at < @idleSince
                """,
                new { agentId, idleSince },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>The agents that have an open conversation.</summary>
    public async Task<IReadOnlyList<Guid>> AgentsWithOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    "SELECT DISTINCT agent_id FROM monitoring_conversations WHERE state = 'Open'",
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    /// <summary>How many of the agent's conversations <see cref="ListAsync"/> pages through with the same filters.</summary>
    public async Task<int> CountAsync(
        Guid agentId,
        string? environment,
        IReadOnlyCollection<Guid> versionIds,
        string? state,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT count(*) FROM monitoring_conversations c {ListFilter}",
                new
                {
                    agentId,
                    environment,
                    versionIds = versionIds.ToArray(),
                    state,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>
    /// A page of the agent's conversations, the newest last turn first, only those of <paramref name="environment"/>, of
    /// <paramref name="versionIds"/> (the version of their last turn) and in <paramref name="state"/> when given.
    /// </summary>
    public async Task<IReadOnlyList<ConversationRow>> ListAsync(
        Guid agentId,
        string? environment,
        IReadOnlyCollection<Guid> versionIds,
        string? state,
        int offset,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var parameters = new
        {
            agentId,
            environment,
            versionIds = versionIds.ToArray(),
            state,
            offset,
            limit,
        };
        var rows = await connection.QueryAsync<ConversationRow>(
            new CommandDefinition(
                $"""
                {ConversationSelect}
                {ListFilter}
                ORDER BY c.last_turn_at DESC, c.id
                OFFSET @offset LIMIT @limit
                """,
                parameters,
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>The environments the agent's conversations came from, sorted.</summary>
    public async Task<IReadOnlyList<string>> EnvironmentsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<string>(
                new CommandDefinition(
                    "SELECT DISTINCT environment FROM monitoring_conversations WHERE agent_id = @agentId ORDER BY environment",
                    new { agentId },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    public async Task<ConversationRow?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ConversationRow>(
            new CommandDefinition($"{ConversationSelect} WHERE c.id = @id", new { id }, cancellationToken: cancellationToken)
        );
    }

    /// <summary>The conversation's turns, oldest first.</summary>
    public async Task<IReadOnlyList<TurnRow>> TurnsAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<TurnRow>(
                new CommandDefinition(
                    """
                    SELECT id, at, agent_version_id AS AgentVersionId, data_version AS DataVersion, items::text AS Items
                    FROM monitoring_turns WHERE conversation_id = @conversationId ORDER BY at, received_at, id
                    """,
                    new { conversationId },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }
}
