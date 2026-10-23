using Dapper;
using Suwageval.Server.Database;

namespace Suwageval.Server.Runs.Unstable;

/// <summary>What the unstable checks read of the runs' results. Plain SQL, mapped by Dapper.</summary>
internal sealed class UnstableStore(Pool pool)
{
    /// <summary>
    /// Per check, scenario and run the samples with a verdict and the passed ones, for the given effective runs. A row carries the
    /// original run's id, time and version and the verdicts of the run it shows (<see cref="EffectiveRuns.Point.Shown"/>). The threshold
    /// is the one of the check revision that ran. Samples where the agent gave no answer (status <c>Failed</c>) are left out.
    /// </summary>
    public async Task<IReadOnlyList<RunCheckVerdictRow>> ListRunCheckVerdictsAsync(
        IReadOnlyCollection<EffectiveRuns.Point> points,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RunCheckVerdictRow>(
            new CommandDefinition(
                """
                SELECT r.id AS RunId, r.created_at AS CreatedAt, r.agent_version_id AS AgentVersionId, s.key AS ScenarioKey,
                       c.key AS CheckKey, cr.check_revision AS CheckRevision, count(*)::int AS Samples,
                       (count(*) FILTER (WHERE cr.passed))::int AS SamplesPassed,
                       COALESCE((rev.content->>'passThreshold')::float8, 1.0) AS PassThreshold
                FROM unnest(@runIds::uuid[], @shownIds::uuid[]) AS p(run_id, shown_id)
                JOIN runs r ON r.id = p.run_id
                JOIN scenario_results sr ON sr.run_id = p.shown_id AND sr.status = 'Done'
                JOIN scenarios s ON s.id = sr.scenario_id
                JOIN check_results cr ON cr.scenario_result_id = sr.id
                JOIN checks c ON c.id = cr.check_id
                JOIN check_revisions rev ON rev.check_id = cr.check_id AND rev.revision = cr.check_revision
                GROUP BY r.id, r.created_at, r.agent_version_id, s.key, c.key, cr.check_revision, rev.content->>'passThreshold'
                """,
                new
                {
                    runIds = points.Select(point => point.Run.Id).ToArray(),
                    shownIds = points.Select(point => point.Shown.Id).ToArray(),
                },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }
}
