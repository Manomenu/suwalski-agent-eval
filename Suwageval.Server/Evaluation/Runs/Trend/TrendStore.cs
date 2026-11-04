using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Evaluation.Runs.Trend;

internal sealed class TrendStore(Pool pool)
{
    public async Task<IReadOnlyList<TrendCheckRow>> ListTrendChecksAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<TrendCheckRow>(
            new CommandDefinition(
                """
                SELECT sr.run_id AS RunId, s.key AS ScenarioKey, c.key AS CheckKey, count(*)::int AS Samples,
                       (count(*) FILTER (WHERE cr.passed))::int AS SamplesPassed,
                       (array_agg(COALESCE((rev.content->>'passThreshold')::float8, 1.0) ORDER BY cr.check_revision DESC))[1] AS PassThreshold
                FROM scenario_results sr
                JOIN scenarios s ON s.id = sr.scenario_id
                JOIN check_results cr ON cr.scenario_result_id = sr.id
                JOIN checks c ON c.id = cr.check_id
                JOIN check_revisions rev ON rev.check_id = cr.check_id AND rev.revision = cr.check_revision
                WHERE sr.run_id = ANY(@runIds)
                GROUP BY sr.run_id, s.key, c.key
                """,
                new { runIds = runIds.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    public async Task<IReadOnlyList<TrendScenarioRow>> ListTrendScenariosAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<TrendScenarioRow>(
            new CommandDefinition(
                """
                SELECT DISTINCT sr.run_id AS RunId, s.key AS ScenarioKey
                FROM scenario_results sr
                JOIN scenarios s ON s.id = sr.scenario_id
                WHERE sr.run_id = ANY(@runIds)
                """,
                new { runIds = runIds.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    public async Task<IReadOnlyList<TrendDurationRow>> ListTrendDurationsAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<TrendDurationRow>(
            new CommandDefinition(
                """
                SELECT sr.run_id AS RunId, s.key AS ScenarioKey, turn.duration AS DurationMs
                FROM scenario_results sr
                JOIN scenarios s ON s.id = sr.scenario_id
                CROSS JOIN LATERAL unnest(COALESCE(sr.turn_durations_ms, ARRAY[sr.duration_ms])) AS turn(duration)
                WHERE sr.run_id = ANY(@runIds) AND sr.duration_ms IS NOT NULL
                """,
                new { runIds = runIds.ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }
}
