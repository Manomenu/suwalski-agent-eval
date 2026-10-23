using System.Text.Json;
using Dapper;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Database;

namespace Suwageval.Server.Runs;

/// <summary>Runs, their queue and their results. Plain SQL, mapped by Dapper.</summary>
internal sealed class RunStore(Pool pool)
{
    private const string RunSelect = """
        SELECT r.id, r.agent_id AS AgentId, a.name AS AgentName, r.agent_version_id AS AgentVersionId, to_jsonb(r.tags)::text AS TagsJson,
               r.endpoint_url AS EndpointUrl, r.status, r.error, r.samples_total AS SamplesTotal,
               (SELECT count(*)::int FROM scenario_results s WHERE s.run_id = r.id) AS SamplesDone,
               check_totals.passed AS ChecksPassed, check_totals.failed AS ChecksFailed,
               r.contract_changed_during_run AS ContractChangedDuringRun, r.cancel_requested AS CancelRequested,
               r.created_at AS CreatedAt, r.started_at AS StartedAt, r.finished_at AS FinishedAt,
               r.recheck_of AS RecheckOf, r.skipped_scenarios::text AS SkippedScenarios, r.verdicts_from AS VerdictsFrom,
               r.origin::text AS Origin, r.reference AS Reference, r.compared_with AS ComparedWith,
               COALESCE((SELECT jsonb_agg(DISTINCT c.judge_model ORDER BY c.judge_model)::text FROM check_results c
                          JOIN scenario_results s ON s.id = c.scenario_result_id
                          WHERE s.run_id = r.id AND c.judge_model IS NOT NULL), '[]') AS JudgeModels,
               (SELECT count(*)::int FROM scenario_results s WHERE s.run_id = r.id AND s.status = 'Failed') AS SamplesFailed,
               r.run_group AS RunGroup, r.label AS Label
        FROM runs r
        JOIN agents a ON a.id = r.agent_id
        CROSS JOIN LATERAL (
            SELECT (count(*) FILTER (WHERE per_check.reached))::int AS passed, (count(*) FILTER (WHERE NOT per_check.reached))::int AS failed
            FROM (
                SELECT (count(*) FILTER (WHERE cr.passed))::float8 / count(*) + 1e-9
                           >= (array_agg(COALESCE((rev.content->>'passThreshold')::float8, 1.0) ORDER BY cr.check_revision DESC))[1] AS reached
                FROM scenario_results sr
                JOIN check_results cr ON cr.scenario_result_id = sr.id
                JOIN check_revisions rev ON rev.check_id = cr.check_id AND rev.revision = cr.check_revision
                WHERE sr.run_id = r.id
                GROUP BY sr.scenario_id, cr.check_id
            ) per_check
        ) check_totals
        """;

    /// <summary>Puts a run in the queue.</summary>
    public async Task<Guid> EnqueueAsync(
        Guid agentId,
        Guid agentVersionId,
        IReadOnlyCollection<string> tags,
        string endpointUrl,
        Guid? recheckOf = null,
        Guid? verdictsFrom = null,
        RunPlacement? placement = null,
        string? runGroup = null,
        string? label = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // A run started in an archived group puts the group back in use.
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM archived_run_groups WHERE agent_id = @agentId AND name = @runGroup::text",
                new { agentId, runGroup },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        var id = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO runs (agent_id, agent_version_id, tags, endpoint_url, recheck_of, verdicts_from, origin, reference, compared_with, run_group, label)
                VALUES (@agentId, @agentVersionId, @tags, @endpointUrl, @recheckOf, @verdictsFrom, @origin::jsonb, @reference, @comparedWith, @runGroup, @label)
                RETURNING id
                """,
                new
                {
                    agentId,
                    agentVersionId,
                    tags = tags.ToArray(),
                    endpointUrl,
                    recheckOf,
                    verdictsFrom,
                    origin = placement?.Origin is { } origin ? JsonSerializer.Serialize(origin, ContractJson.Options) : null,
                    reference = placement?.Reference ?? false,
                    comparedWith = placement?.ComparedWith,
                    runGroup,
                    label,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<RunRow?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RunRow>(
            new CommandDefinition($"{RunSelect} WHERE r.id = @id", new { id }, cancellationToken: cancellationToken)
        );
    }

    /// <summary>The runs of one agent, newest first.</summary>
    public async Task<IReadOnlyList<RunRow>> ListAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RunRow>(
            new CommandDefinition(
                $"{RunSelect} WHERE r.agent_id = @agentId ORDER BY r.created_at DESC, r.id",
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    private const string ListFilter = $"""
        WHERE (@agentId::uuid IS NULL OR r.agent_id = @agentId)
          AND (@runGroup::text IS NULL OR r.run_group = @runGroup)
          AND (@grouping = 'All'
               OR (@grouping = 'None') = ({RunGroupSql.EffectiveGroup} IS NULL))
          AND (cardinality(@versionIds::uuid[]) = 0 OR r.agent_version_id = ANY(@versionIds::uuid[]))
          AND (cardinality(@tags::text[]) = 0 OR cardinality(r.tags) = 0 OR r.tags && @tags::text[])
        """;

    /// <summary>
    /// A page of runs, newest first (<c>created_at</c> descending, then <c>id</c>): at most <paramref name="limit"/> of them after
    /// the first <paramref name="offset"/>, only those of the given agent versions when <paramref name="versionIds"/> is not empty,
    /// only those of <paramref name="runGroup"/> when one is given, only those in no group or in some group as <paramref name="grouping"/> says
    /// (a re-check counts as in the group of the run it was made from),
    /// only those that ran the scenarios of any of <paramref name="tags"/> when some are given: limited to tags including one of them, or not limited at all.
    /// </summary>
    public async Task<IReadOnlyList<RunRow>> ListPageAsync(
        Guid? agentId,
        IReadOnlyCollection<Guid> versionIds,
        int offset,
        int limit,
        string? runGroup = null,
        RunGrouping grouping = RunGrouping.All,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RunRow>(
            new CommandDefinition(
                $"""
                {RunSelect}
                {ListFilter}
                ORDER BY r.created_at DESC, r.id
                OFFSET @offset LIMIT @limit
                """,
                new
                {
                    agentId,
                    versionIds = versionIds.ToArray(),
                    offset,
                    limit,
                    runGroup,
                    grouping = grouping.ToString(),
                    tags = (tags ?? []).ToArray(),
                },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>How many runs <see cref="ListPageAsync"/> pages through with the same filters.</summary>
    public async Task<int> CountAsync(
        Guid? agentId,
        IReadOnlyCollection<Guid> versionIds,
        string? runGroup = null,
        RunGrouping grouping = RunGrouping.All,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT count(*) FROM runs r {ListFilter}",
                new
                {
                    agentId,
                    versionIds = versionIds.ToArray(),
                    runGroup,
                    grouping = grouping.ToString(),
                    tags = (tags ?? []).ToArray(),
                },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>
    /// Takes the oldest queued run and marks it running — in one statement, with <c>FOR UPDATE SKIP LOCKED</c>, so
    /// two workers never get the same run. Null when the queue is empty. <paramref name="agentId"/> narrows the queue to
    /// one agent's runs (the tests share a database; the worker passes none).
    /// </summary>
    public async Task<RunRow?> ClaimNextAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        Guid? id;
        await using (var connection = await pool.OpenAsync(cancellationToken))
        {
            id = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    """
                    UPDATE runs SET status = 'Running', started_at = now()
                    WHERE id = (
                        SELECT id FROM runs
                        WHERE status = 'Queued' AND (@agentId::uuid IS NULL OR agent_id = @agentId)
                        ORDER BY created_at, id
                        FOR UPDATE SKIP LOCKED
                        LIMIT 1
                    )
                    RETURNING id
                    """,
                    new { agentId },
                    cancellationToken: cancellationToken
                )
            );
        }
        // After the claiming connection is back in the pool: holding two at once could exhaust a small pool.
        return id is { } claimed ? await GetAsync(claimed, cancellationToken) : null;
    }

    public async Task SetTotalAsync(Guid id, int samplesTotal, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE runs SET samples_total = @samplesTotal WHERE id = @id",
                new { id, samplesTotal },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>Records the scenarios a re-check left out.</summary>
    public async Task SetSkippedAsync(Guid id, IReadOnlyList<SkippedScenario> skipped, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE runs SET skipped_scenarios = @skippedJson::jsonb WHERE id = @id",
                new { id, skippedJson = JsonSerializer.Serialize(skipped, ContractJson.Options) },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>
    /// Asks a run to stop: a queued one is cancelled at once, a running one when the worker next looks (before the next
    /// sample). A finished run stays as it is.
    /// </summary>
    public async Task RequestCancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE runs
                SET cancel_requested = true,
                    status = CASE WHEN status = 'Queued' THEN 'Cancelled' ELSE status END,
                    finished_at = CASE WHEN status = 'Queued' THEN now() ELSE finished_at END
                WHERE id = @id AND status IN ('Queued', 'Running')
                """,
                new { id },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>Records that the agent's contract changed while the run was going.</summary>
    public async Task MarkContractChangedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE runs SET contract_changed_during_run = true WHERE id = @id",
                new { id },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<bool> IsCancelRequestedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition("SELECT cancel_requested FROM runs WHERE id = @id", new { id }, cancellationToken: cancellationToken)
        );
    }

    public async Task FinishAsync(Guid id, RunStatus status, string? error = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE runs SET status = @status, error = @error, finished_at = now() WHERE id = @id",
                new
                {
                    id,
                    status = status.ToString(),
                    error,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    /// <summary>
    /// Marks every run still <c>Running</c> as <c>Interrupted</c> (finished now, with the reason in its error) and says how many there
    /// were. Called when the worker starts: the chart and compose run one server (<c>replicas: 1</c>), so after a start no other process
    /// can hold a run — with more replicas a worker heartbeat would be needed instead. <paramref name="agentId"/> narrows it to one
    /// agent's runs (the tests share a database; the worker passes none).
    /// </summary>
    public async Task<int> InterruptRunningAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE runs
                SET status = 'Interrupted', finished_at = now(), error = @error
                WHERE status = 'Running' AND (@agentId::uuid IS NULL OR agent_id = @agentId)
                """,
                new { agentId, error = InterruptedError },
                cancellationToken: cancellationToken
            )
        );
    }

    public const string InterruptedError = "Interrupted: the server stopped during the run; start it again with rerun.";

    /// <summary>Stores a finished sample and the verdicts of its checks, all or nothing.</summary>
    public async Task AddSampleAsync(Guid runId, NewSample sample, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var resultId = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO scenario_results (run_id, scenario_id, scenario_revision, sample, status, output, error, undeclared, duration_ms,
                                              turn_durations_ms, ended_by_user)
                VALUES (@runId, @scenarioId, @scenarioRevision, @sampleNumber, @status, @outputJson::jsonb, @error, @undeclaredJson::jsonb, @durationMs,
                        @turnDurationsMs, @endedByUser)
                RETURNING id
                """,
                new
                {
                    runId,
                    scenarioId = sample.ScenarioId,
                    scenarioRevision = sample.ScenarioRevision,
                    sampleNumber = sample.Sample,
                    status = sample.Status.ToString(),
                    outputJson = sample.OutputJson,
                    error = sample.Error,
                    durationMs = sample.DurationMs,
                    turnDurationsMs = sample.TurnDurationsMs?.ToArray(),
                    endedByUser = sample.EndedByUser,
                    undeclaredJson = JsonSerializer.Serialize(sample.Undeclared, ContractJson.Options),
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        foreach (var check in sample.Checks)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO check_results (scenario_result_id, check_id, check_revision, passed, not_judged, judge_model, evidence, message)
                    VALUES (@resultId, @checkId, @checkRevision, @passed, @notJudged, @judgeModel, @evidenceJson::jsonb, @message)
                    """,
                    new
                    {
                        resultId,
                        checkId = check.CheckId,
                        checkRevision = check.CheckRevision,
                        passed = check.Passed,
                        notJudged = check.NotJudged,
                        judgeModel = check.JudgeModel,
                        evidenceJson = check.EvidenceJson,
                        message = check.Message,
                    },
                    transaction,
                    cancellationToken: cancellationToken
                )
            );
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>The samples stored so far, by scenario key and sample number.</summary>
    public async Task<IReadOnlyList<ScenarioResultRow>> ListScenarioResultsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ScenarioResultRow>(
            new CommandDefinition(
                """
                SELECT sr.id, s.key AS ScenarioKey, sr.scenario_revision AS ScenarioRevision, sr.sample, sr.status,
                       sr.output::text AS Output, sr.error, sr.undeclared::text AS Undeclared,
                       sr.duration_ms AS DurationMs, sr.scenario_id AS ScenarioId,
                       rev.content::text AS ScenarioContent, to_jsonb(s.tags)::text AS ScenarioTagsJson,
                       to_jsonb(sr.turn_durations_ms)::text AS TurnDurationsJson, sr.ended_by_user AS EndedByUser
                FROM scenario_results sr
                JOIN scenarios s ON s.id = sr.scenario_id
                JOIN scenario_revisions rev ON rev.scenario_id = sr.scenario_id AND rev.revision = sr.scenario_revision
                WHERE sr.run_id = @runId
                ORDER BY s.key, sr.sample
                """,
                new { runId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>
    /// The history of every check of run <paramref name="runId"/>, in one query: per check (and scenario) the newest <paramref name="limit"/>
    /// runs it has a verdict in, oldest first, each with its samples, the passed ones and the threshold of the check revision that ran.
    /// The runs are those that called the agent (never a re-check), finished and created before <paramref name="anchorRunId"/> (the run
    /// that called the agent, when <paramref name="runId"/> is a re-check of it), of the same agent; when <paramref name="comparedWith"/> is
    /// set only reference runs of that branch, otherwise every such run. A run's newest finished re-check supplies the verdicts that
    /// count (<see cref="EffectiveRuns"/>). Samples where the agent gave no answer (status <c>Failed</c>) are left out: a run whose samples all
    /// failed is no point of the history, and a partly failed one is counted over its other samples. Only verdicts of the same check revision in the same scenario revision as the run's own count.
    /// </summary>
    public async Task<IReadOnlyList<RegressionHistoryRow>> ListCheckHistoriesAsync(
        Guid runId,
        Guid anchorRunId,
        string? comparedWith,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RegressionHistoryRow>(
            new CommandDefinition(
                """
                WITH anchor AS (SELECT agent_id, created_at FROM runs WHERE id = @anchorRunId),
                mine AS (
                    SELECT DISTINCT sr.scenario_id, sr.scenario_revision, cr.check_id, cr.check_revision
                    FROM scenario_results sr
                    JOIN check_results cr ON cr.scenario_result_id = sr.id
                    WHERE sr.run_id = @runId
                ),
                candidates AS (
                    SELECT c.id, c.created_at,
                           COALESCE((SELECT x.id FROM runs x WHERE x.recheck_of = c.id AND x.status = 'Done'
                                     ORDER BY x.created_at DESC, x.id LIMIT 1), c.id) AS shown_id
                    FROM runs c
                    JOIN anchor a ON c.agent_id = a.agent_id AND c.created_at < a.created_at
                    WHERE c.status = 'Done' AND c.recheck_of IS NULL
                      AND c.run_group IS NULL
                      AND (@comparedWith::text IS NULL OR (c.reference AND c.origin ->> 'branch' = @comparedWith))
                ),
                per_run AS (
                    SELECT c.id AS run_id, c.created_at, sr.scenario_id, cr.check_id,
                           count(*)::int AS samples, (count(*) FILTER (WHERE cr.passed))::int AS samples_passed,
                           COALESCE((rev.content ->> 'passThreshold')::float8, 1.0) AS pass_threshold
                    FROM candidates c
                    JOIN scenario_results sr ON sr.run_id = c.shown_id AND sr.status = 'Done'
                    JOIN check_results cr ON cr.scenario_result_id = sr.id
                    JOIN check_revisions rev ON rev.check_id = cr.check_id AND rev.revision = cr.check_revision
                    JOIN mine m ON m.scenario_id = sr.scenario_id AND m.scenario_revision = sr.scenario_revision
                               AND m.check_id = cr.check_id AND m.check_revision = cr.check_revision
                    GROUP BY c.id, c.created_at, sr.scenario_id, cr.check_id, rev.content ->> 'passThreshold'
                ),
                ranked AS (
                    SELECT p.*, row_number() OVER (PARTITION BY p.scenario_id, p.check_id ORDER BY p.created_at DESC, p.run_id) AS rn
                    FROM per_run p
                )
                SELECT scenario_id AS ScenarioId, check_id AS CheckId, run_id AS RunId, created_at AS CreatedAt, samples AS Samples,
                       samples_passed AS SamplesPassed, pass_threshold AS PassThreshold
                FROM ranked
                WHERE rn <= @limit
                ORDER BY scenario_id, check_id, created_at, run_id
                """,
                new
                {
                    runId,
                    anchorRunId,
                    comparedWith,
                    limit,
                },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }

    /// <summary>The verdicts stored so far. The threshold is the one of the check revision that ran.</summary>
    public async Task<IReadOnlyList<CheckResultRow>> ListCheckResultsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CheckResultRow>(
            new CommandDefinition(
                """
                SELECT cr.scenario_result_id AS ScenarioResultId, c.key AS CheckKey, cr.check_revision AS CheckRevision,
                       (SELECT MAX(latest.revision) FROM check_revisions latest WHERE latest.check_id = cr.check_id) AS LatestRevision,
                       rev.content::text AS Definition,
                       COALESCE((rev.content->>'passThreshold')::float8, 1.0) AS PassThreshold,
                       cr.passed, cr.not_judged AS NotJudged, cr.evidence::text AS Evidence, cr.message, cr.check_id AS CheckId, cr.judge_model AS JudgeModel
                FROM check_results cr
                JOIN scenario_results sr ON sr.id = cr.scenario_result_id
                JOIN checks c ON c.id = cr.check_id
                JOIN check_revisions rev ON rev.check_id = cr.check_id AND rev.revision = cr.check_revision
                WHERE sr.run_id = @runId
                ORDER BY c.key
                """,
                new { runId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
    }
}
