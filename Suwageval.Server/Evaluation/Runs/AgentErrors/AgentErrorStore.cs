using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Evaluation.Runs.AgentErrors;

/// <summary>What the agent errors read of the runs' results. Plain SQL, mapped by Dapper.</summary>
internal sealed class AgentErrorStore(Pool pool)
{
    /// <summary>
    /// Per agent version and scenario of the given runs (the ones that called the agent, so the original of a re-check): the samples asked and
    /// those where the agent gave no answer (status <c>Failed</c>), with the newest such sample's run and error (none without errors).
    /// The highest share of errors first, then the most, then by key.
    /// </summary>
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
