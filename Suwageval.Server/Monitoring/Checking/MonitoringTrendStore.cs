using Dapper;
using Suwageval.Contracts.Monitoring;
using Suwageval.Server.Database;

namespace Suwageval.Server.Monitoring.Checking;

/// <summary>What the Monitoring page's charts read: the agent's conversations and verdicts day by day. Plain SQL, mapped by Dapper.</summary>
internal sealed class MonitoringTrendStore(Pool pool)
{
    private const string Conversations = """
        FROM monitoring_conversations c
        WHERE c.agent_id = @agentId AND c.last_turn_at >= @since
          AND (@environment::text IS NULL OR c.environment = @environment)
        """;

    /// <summary>The trend of the agent's conversations whose last turn came since <paramref name="since"/> (UTC days), of one environment when given.</summary>
    public async Task<MonitoringTrend> TrendAsync(
        Guid agentId,
        DateTimeOffset since,
        string? environment,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var parameters = new
        {
            agentId,
            since,
            environment,
        };
        var days = await connection.QueryAsync<MonitoringDay>(
            new CommandDefinition(
                $"""
                SELECT to_char(c.last_turn_at AT TIME ZONE 'UTC', 'YYYY-MM-DD') AS Date, count(*)::int AS Conversations,
                       (count(*) FILTER (WHERE c.checked_at IS NOT NULL))::int AS Checked
                {Conversations}
                GROUP BY 1 ORDER BY 1
                """,
                parameters,
                cancellationToken: cancellationToken
            )
        );
        var points = await connection.QueryAsync<MonitoringPoint>(
            new CommandDefinition(
                """
                SELECT to_char(c.last_turn_at AT TIME ZONE 'UTC', 'YYYY-MM-DD') AS Date, k.key AS CheckKey, c.agent_version_id AS AgentVersionId,
                       (count(*) FILTER (WHERE r.passed))::int AS Passed, count(*)::int AS Checked
                FROM monitoring_conversations c
                JOIN monitoring_check_results r ON r.conversation_id = c.id AND NOT r.not_judged
                JOIN monitoring_checks k ON k.id = r.check_id
                WHERE c.agent_id = @agentId AND c.last_turn_at >= @since
                  AND (@environment::text IS NULL OR c.environment = @environment)
                GROUP BY 1, 2, 3 ORDER BY 1, 2, 3
                """,
                parameters,
                cancellationToken: cancellationToken
            )
        );
        var versions = await connection.QueryAsync<(DateTime At, Guid AgentVersionId, string? Label)>(
            new CommandDefinition(
                $"""
                SELECT min(t.at) AS At, t.agent_version_id AS AgentVersionId, v.label AS Label
                FROM monitoring_turns t
                JOIN monitoring_conversations c ON c.id = t.conversation_id
                JOIN agent_versions v ON v.id = t.agent_version_id
                WHERE c.agent_id = @agentId AND t.at >= @since AND (@environment::text IS NULL OR c.environment = @environment)
                GROUP BY t.agent_version_id, v.label ORDER BY 1
                """,
                parameters,
                cancellationToken: cancellationToken
            )
        );
        var data = await connection.QueryAsync<(DateTime At, string DataVersion)>(
            new CommandDefinition(
                """
                SELECT min(t.at) AS At, t.data_version AS DataVersion
                FROM monitoring_turns t
                JOIN monitoring_conversations c ON c.id = t.conversation_id
                WHERE c.agent_id = @agentId AND t.at >= @since AND t.data_version IS NOT NULL
                  AND (@environment::text IS NULL OR c.environment = @environment)
                GROUP BY t.data_version ORDER BY 1
                """,
                parameters,
                cancellationToken: cancellationToken
            )
        );
        return new MonitoringTrend
        {
            Days = [.. days],
            Points = [.. points],
            Versions = [.. versions.Select(version => new VersionChange(version.At, version.AgentVersionId, version.Label))],
            DataVersions = [.. data.Select(change => new DataChange(change.At, change.DataVersion))],
        };
    }
}
