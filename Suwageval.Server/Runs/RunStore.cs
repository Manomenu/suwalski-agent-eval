using System.Text.Json;
using Dapper;
using Suwageval.Contracts;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Database;

namespace Suwageval.Server.Runs;

/// <summary>Runs, their queue and their results. Plain SQL, mapped by Dapper.</summary>
internal sealed class RunStore(Pool pool)
{
    private const string RunSelect = """
        SELECT r.id, r.agent_id AS AgentId, a.name AS AgentName, r.agent_version_id AS AgentVersionId, r.tag,
               r.endpoint_url AS EndpointUrl, r.status, r.error, r.samples_total AS SamplesTotal,
               (SELECT count(*)::int FROM scenario_results s WHERE s.run_id = r.id) AS SamplesDone,
               (SELECT count(*)::int FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id
                 WHERE s.run_id = r.id AND c.passed) AS ChecksPassed,
               (SELECT count(*)::int FROM check_results c JOIN scenario_results s ON s.id = c.scenario_result_id
                 WHERE s.run_id = r.id AND NOT c.passed) AS ChecksFailed,
               r.contract_changed_during_run AS ContractChangedDuringRun,
               r.created_at AS CreatedAt, r.started_at AS StartedAt, r.finished_at AS FinishedAt
        FROM runs r
        JOIN agents a ON a.id = r.agent_id
        """;

    /// <summary>Puts a run in the queue.</summary>
    public async Task<Guid> EnqueueAsync(
        Guid agentId,
        Guid agentVersionId,
        string? tag,
        string endpointUrl,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO runs (agent_id, agent_version_id, tag, endpoint_url)
                VALUES (@agentId, @agentVersionId, @tag, @endpointUrl)
                RETURNING id
                """,
                new
                {
                    agentId,
                    agentVersionId,
                    tag,
                    endpointUrl,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    public async Task<RunRow?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RunRow>(
            new CommandDefinition($"{RunSelect} WHERE r.id = @id", new { id }, cancellationToken: cancellationToken)
        );
    }

    /// <summary>Newest first, optionally only the runs of one agent.</summary>
    public async Task<IReadOnlyList<RunRow>> ListAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<RunRow>(
            new CommandDefinition(
                $"{RunSelect} WHERE (@agentId::uuid IS NULL OR r.agent_id = @agentId) ORDER BY r.created_at DESC, r.id",
                new { agentId },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows];
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

    /// <summary>Stores a finished sample and the verdicts of its checks, all or nothing.</summary>
    public async Task AddSampleAsync(Guid runId, NewSample sample, CancellationToken cancellationToken = default)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var resultId = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO scenario_results (run_id, scenario_id, scenario_revision, sample, status, output, error, undeclared)
                VALUES (@runId, @scenarioId, @scenarioRevision, @sampleNumber, @status, @outputJson::jsonb, @error, @undeclaredJson::jsonb)
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
                    INSERT INTO check_results (scenario_result_id, check_id, check_revision, passed, evidence, message)
                    VALUES (@resultId, @checkId, @checkRevision, @passed, @evidenceJson::jsonb, @message)
                    """,
                    new
                    {
                        resultId,
                        checkId = check.CheckId,
                        checkRevision = check.CheckRevision,
                        passed = check.Passed,
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
                       sr.output::text AS Output, sr.error, sr.undeclared::text AS Undeclared
                FROM scenario_results sr
                JOIN scenarios s ON s.id = sr.scenario_id
                WHERE sr.run_id = @runId
                ORDER BY s.key, sr.sample
                """,
                new { runId },
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
                       COALESCE((rev.content->>'passThreshold')::float8, 1.0) AS PassThreshold,
                       cr.passed, cr.evidence::text AS Evidence, cr.message
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
