using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Monitoring.Checking;

/// <summary>What the monitoring worker reads and writes: the conversations to check, the checks to run, the verdicts. Plain SQL, mapped by Dapper.</summary>
internal sealed class CheckingStore(Pool pool)
{
    /// <summary>A live monitoring check with its latest revision (the check as pushed, JSON).</summary>
    internal sealed record CheckToRun(Guid Id, int Revision, string Content);

    /// <summary>One verdict to store.</summary>
    internal sealed record Verdict(
        Guid CheckId,
        int Revision,
        bool Passed,
        bool NotJudged,
        string Message,
        string Evidence,
        string? JudgeModel
    );

    /// <summary>The closed conversations no check ran on yet, the longest closed first: (conversation, agent).</summary>
    public async Task<IReadOnlyList<(Guid Id, Guid AgentId)>> UncheckedAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<(Guid, Guid)>(
                new CommandDefinition(
                    """
                    SELECT id, agent_id FROM monitoring_conversations
                    WHERE state = 'Closed' AND checked_at IS NULL
                    ORDER BY closed_at, id
                    LIMIT @limit
                    """,
                    new { limit },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    /// <summary>The agent's live monitoring checks, by key.</summary>
    public async Task<IReadOnlyList<CheckToRun>> ChecksAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<CheckToRun>(
                new CommandDefinition(
                    """
                    SELECT c.id, r.revision, r.content::text AS Content
                    FROM monitoring_checks c
                    JOIN LATERAL (
                        SELECT * FROM monitoring_check_revisions WHERE check_id = c.id ORDER BY revision DESC LIMIT 1
                    ) r ON true
                    WHERE c.agent_id = @agentId AND c.deleted_at IS NULL
                    ORDER BY c.key
                    """,
                    new { agentId },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    /// <summary>The definitions of the monitoring checks the conversation failed (not those not judged), in the revision that failed, by key.</summary>
    public async Task<IReadOnlyList<string>> FailedChecksAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return
        [
            .. await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT r.content::text
                    FROM monitoring_check_results res
                    JOIN monitoring_checks c ON c.id = res.check_id
                    JOIN monitoring_check_revisions r ON r.check_id = res.check_id AND r.revision = res.revision
                    WHERE res.conversation_id = @conversationId AND NOT res.passed AND NOT res.not_judged
                    ORDER BY c.key
                    """,
                    new { conversationId },
                    cancellationToken: cancellationToken
                )
            ),
        ];
    }

    /// <summary>
    /// Stores the verdicts as the conversation's, replacing those it had, and marks it checked — unless a turn opened it again meanwhile
    /// (then nothing is stored: it is checked when it closes again). True when stored.
    /// </summary>
    public async Task<bool> StoreAsync(Guid conversationId, IReadOnlyList<Verdict> verdicts, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var closed = await connection.QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition(
                "SELECT state = 'Closed' FROM monitoring_conversations WHERE id = @conversationId FOR UPDATE",
                new { conversationId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        if (!closed)
        {
            return false;
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM monitoring_check_results WHERE conversation_id = @conversationId",
                new { conversationId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        foreach (var verdict in verdicts)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO monitoring_check_results (conversation_id, check_id, revision, passed, not_judged, message, evidence, judge_model)
                    VALUES (@conversationId, @checkId, @revision, @passed, @notJudged, @message, @evidence::jsonb, @judgeModel)
                    """,
                    new
                    {
                        conversationId,
                        checkId = verdict.CheckId,
                        revision = verdict.Revision,
                        passed = verdict.Passed,
                        notJudged = verdict.NotJudged,
                        message = verdict.Message,
                        evidence = verdict.Evidence,
                        judgeModel = verdict.JudgeModel,
                    },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
        }
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE monitoring_conversations SET checked_at = now() WHERE id = @conversationId",
                new { conversationId },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
