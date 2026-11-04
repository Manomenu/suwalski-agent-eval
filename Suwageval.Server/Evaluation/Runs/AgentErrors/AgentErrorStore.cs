using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Evaluation.Runs.AgentErrors;

internal sealed class AgentErrorStore(Pool pool)
{
    public async Task<IReadOnlyList<AgentErrorRow>> ListAgentErrorsAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AgentErrorRow>(
            new CommandDefinition(
                """
                SELECT r.agent_version_id AS AgentVersionId, s.key AS ScenarioKey, count(*)::int AS Samples,
                       (count(*) FILTER (WHERE sr.status = 'Failed'))::int AS Errors,
                       (array_agg(r.id ORDER BY r.created_at DESC, r.id, sr.sample) FILTER (WHERE sr.status = 'Failed'))[1] AS NewestErrorRunId,
                       (array_agg(COALESCE(sr.error, '') ORDER BY r.created_at DESC, r.id, sr.sample) FILTER (WHERE sr.status = 'Failed'))[1] AS NewestError
                FROM runs r
                JOIN scenario_results sr ON sr.run_id = r.id
                JOIN scenarios s ON s.id = sr.scenario_id
                WHERE r.id = ANY(@runIds)
                GROUP BY r.agent_version_id, s.key
                ORDER BY (count(*) FILTER (WHERE sr.status = 'Failed'))::float8 / count(*) DESC,
                         count(*) FILTER (WHERE sr.status = 'Failed') DESC, s.key, r.agent_version_id
                """,
                new { runIds = runIds.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }
}
