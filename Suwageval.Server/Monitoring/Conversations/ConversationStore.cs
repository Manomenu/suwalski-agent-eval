using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Monitoring;
using Suwageval.Server.Database;

namespace Suwageval.Server.Monitoring.Conversations;

internal sealed class ConversationStore(Pool pool)
{
    private const string ConversationSelect = """
        SELECT c.id, a.name AS AgentName, c.environment, c.conversation_key AS ConversationKey, c.agent_version_id AS AgentVersionId,
               v.label AS VersionLabel, c.version_changed AS VersionChanged, c.data_version AS DataVersion, c.state, c.turns,
               c.started_at AS StartedAt, c.last_turn_at AS LastTurnAt, c.closed_at AS ClosedAt, c.checked_at AS CheckedAt,
               (SELECT count(*) FROM monitoring_check_results r WHERE r.conversation_id = c.id AND NOT r.passed AND NOT r.not_judged)::int AS FailedChecks,
               (SELECT count(*) FROM monitoring_check_results r WHERE r.conversation_id = c.id AND r.not_judged)::int AS NotJudgedChecks
        FROM monitoring_conversations c
        JOIN agents a ON a.id = c.agent_id
        JOIN agent_versions v ON v.id = c.agent_version_id
        """;

    private const string ListFilter = """
        WHERE c.agent_id = @agentId
          AND (@environment::text IS NULL OR c.environment = @environment)
          AND (cardinality(@versionIds::uuid[]) = 0 OR c.agent_version_id = ANY(@versionIds::uuid[]))
          AND (@state::text IS NULL OR c.state = @state)
          AND (@failedCheck::text IS NULL OR EXISTS (
                SELECT 1 FROM monitoring_check_results r JOIN monitoring_checks k ON k.id = r.check_id
                WHERE r.conversation_id = c.id AND k.key = @failedCheck AND NOT r.passed AND NOT r.not_judged))
        """;

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
                        checked_at = NULL,
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
            // Why: verdicts belong to a checked conversation; an open one is unchecked, and kept rows would be counted, filtered and shown as if they judged the new turns
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM monitoring_check_results WHERE conversation_id = @conversationId",
                    new { conversationId },
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

    public async Task<int> CountAsync(
        Guid agentId,
        string? environment,
        IReadOnlyCollection<Guid> versionIds,
        string? state,
        string? failedCheck = null,
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
                    failedCheck,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<IReadOnlyList<ConversationRow>> ListAsync(
        Guid agentId,
        string? environment,
        IReadOnlyCollection<Guid> versionIds,
        string? state,
        int offset,
        int limit,
        string? failedCheck = null,
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
            failedCheck,
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

    public async Task<IReadOnlyList<ConversationResultRow>> ResultsAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<ConversationResultRow>(
                new CommandDefinition(
                    """
                    SELECT k.key, r.revision, r.passed, r.not_judged AS NotJudged, r.message, r.evidence::text AS Evidence, r.judge_model AS JudgeModel
                    FROM monitoring_check_results r JOIN monitoring_checks k ON k.id = r.check_id
                    WHERE r.conversation_id = @conversationId
                    ORDER BY k.key
                    """,
                    new { conversationId },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }
}
