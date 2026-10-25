using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Invocation;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;
using Suwageval.Server.Problems;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

/// <summary>The routes of runs: start one or repeat one, follow it, read its results, cancel it.</summary>
internal static class RunEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapRuns(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/runs", CreateAsync).Produces<RunCreated>(StatusCodes.Status202Accepted);
        app.MapGet("/runs", ListAsync).Produces<RunList>();
        app.MapGet("/runs/{id:guid}", GetAsync).Produces<RunSummary>();
        app.MapGet("/runs/{id:guid}/results", ResultsAsync).Produces<RunResults>();
        app.MapPost("/runs/{id:guid}/rerun", RerunAsync).Produces<RunCreated>(StatusCodes.Status202Accepted);
        app.MapGet("/runs/{id:guid}/recheck", RecheckPlanAsync).Produces<RecheckPlan>();
        app.MapPost("/runs/{id:guid}/recheck", RecheckAsync).Produces<RunCreated>(StatusCodes.Status202Accepted);
        app.MapPost("/runs/{id:guid}/cancel", CancelAsync).Produces(StatusCodes.Status202Accepted);
        app.MapPut("/runs/{id:guid}/note", SetNoteAsync).Produces(StatusCodes.Status204NoContent);
        return app;
    }

    /// <summary>
    /// 202 with the run's id and its version once it is queued: the version is the contract the agent serves at that moment,
    /// registered here. 404: no such agent; 422: the request or what it selects is not runnable (a bad endpoint, no scenario
    /// with the tag); 502: the agent's contract could not be read or is not valid; 409: the version does not fit the scenarios
    /// and checks — the mismatches are listed and nothing is queued; 409 also when a judge check or a <c>$judge</c> argument matcher is
    /// picked and no judge model is configured, or a simulated scenario and no simulator model.
    /// </summary>
    private static Task<IResult> CreateAsync(
        RunRequest request,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        AgentClient client,
        Judge judge,
        UserSimulator simulator,
        CancellationToken cancellationToken
    ) => StartAsync(request, null, agents, scenarios, checks, runs, client, judge, simulator, cancellationToken);

    /// <summary>
    /// <see cref="CreateAsync"/> with the placement of the run (origin, reference, compared branch) given by the caller — a rerun's is its
    /// source's; none decides it from the request's origin and the agent's regression settings.
    /// </summary>
    private static async Task<IResult> StartAsync(
        RunRequest request,
        RunPlacement? inherited,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        AgentClient client,
        Judge judge,
        UserSimulator simulator,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Agent))
        {
            return ProblemResults.Invalid("agent", "The agent's name or id is required.");
        }
        if (request.EndpointUrl is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"))
        {
            return ProblemResults.Invalid("endpointUrl", "The endpoint is an absolute http or https URL.");
        }

        if (RunGroupRules.Problem(request.Group) is { } groupProblem)
        {
            return ProblemResults.Invalid("group", groupProblem);
        }

        if (NoteRules.Problem(request.Note) is { } noteProblem)
        {
            return ProblemResults.Invalid("note", noteProblem);
        }

        var agent = await agents.GetAsync(request.Agent, cancellationToken);
        if (agent is null)
        {
            return ProblemResults.NotFound("Agent", request.Agent);
        }
        if (!agent.Evaluation)
        {
            return ProblemResults.FeatureOff(agent.Name, "evaluation");
        }

        var picked = await scenarios.ListAsync(agent.Id, request.Tags, cancellationToken);
        if (picked.Count == 0)
        {
            return ProblemResults.Invalid(
                "tags",
                request.Tags.Count == 0
                    ? "The agent has no scenarios."
                    : $"No scenario has any of the tags {string.Join(", ", request.Tags.Select(tag => $"'{tag}'"))}."
            );
        }

        // The version is what the agent says it is right now: read its contract, register it (the same content is the same
        // version), and run against that. Nothing is queued when the agent cannot say.
        var endpoint = request.EndpointUrl ?? agent.EndpointUrl;
        var served = await client.GetContractAsync(new Uri(endpoint), cancellationToken);
        if (served.Contract is not { } contract)
        {
            return ProblemResults.BadGateway("The agent's contract could not be read", served.Error ?? "The agent served nothing.");
        }
        var contractProblems = AgentContractRules.Problems(contract);
        if (contractProblems.Count > 0)
        {
            return ProblemResults.BadGateway(
                "The agent's contract is not valid",
                $"The contract served at {endpoint.TrimEnd('/')}/suwageval/contract is not valid: {string.Join("; ", contractProblems)}."
            );
        }
        var registration = await agents.RegisterVersionAsync(
            agent.Id,
            AgentContractHash.Content(contract),
            contract.Label,
            cancellationToken
        );
        var version = registration.Version;

        var keys = picked.Select(scenario => scenario.Key).ToHashSet();
        var pickedChecks = (await checks.ListAsync(agent.Id, cancellationToken: cancellationToken))
            .Where(check => keys.Contains(check.ScenarioKey))
            .Select(StoredContent.Check)
            .ToList();

        var mismatches = RunCompatibility.Mismatches(
            ContractContent.Read(version.Content),
            [.. picked.Select(scenario => (scenario.Key, StoredContent.Scenario(scenario.Content)))],
            pickedChecks
        );
        if (mismatches.Count > 0)
        {
            return Results.Problem(
                title: "The version does not fit the scenarios",
                detail: string.Join("; ", mismatches),
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["mismatches"] = mismatches }
            );
        }

        if (!judge.IsConfigured && pickedChecks.Any(CheckEvaluator.JudgedByModel))
        {
            return Results.Problem(
                title: "No judge model is configured",
                detail: "The scenarios have judge checks or $judge argument matching, but no model is configured for the judge (Models:fallback, or Models:judge of its own: BaseUrl and Model); nothing was queued.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        if (!simulator.IsConfigured && picked.Any(scenario => StoredContent.Scenario(scenario.Content).Simulated))
        {
            return Results.Problem(
                title: "No simulator model is configured",
                detail: "The scenarios have a simulated user, but no model is configured for the simulated user (Models:fallback, or Models:simulator of its own: BaseUrl and Model); nothing was queued.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        var placement = inherited ?? RunPlacement.Decide(request.Origin, agent.Regression);
        if (request.Group is not null)
        {
            placement = placement.AsExperiment();
        }
        var id = await runs.EnqueueAsync(
            agent.Id,
            version.Id,
            request.Tags,
            endpoint,
            placement: placement,
            runGroup: request.Group,
            label: contract.Label,
            note: NoteRules.Normalize(request.Note),
            dataVersion: contract.Context.Retrieval?.DataVersion,
            dataDescription: contract.Context.Retrieval?.Description,
            cancellationToken: cancellationToken
        );
        return Results.Accepted(
            $"/eval/runs/{id}",
            new RunCreated
            {
                RunId = id,
                AgentVersionId = version.Id,
                VersionCreated = registration.Created,
            }
        );
    }

    /// <summary>
    /// A new run with the parameters of run <paramref name="id"/> — its agent, tags and endpoint (unless the body gives another) —
    /// through <see cref="CreateAsync"/>: the version is what the agent serves now, the scenarios its current ones, and the
    /// answers and refusals are the same. 404 for an unknown run; the repeated run is not touched.
    /// </summary>
    private static async Task<IResult> RerunAsync(
        Guid id,
        RerunRequest? request,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        AgentClient client,
        Judge judge,
        UserSimulator simulator,
        CancellationToken cancellationToken
    )
    {
        var original = await runs.GetAsync(id, cancellationToken);
        if (original is null)
        {
            return ProblemResults.NotFound("Run", id.ToString());
        }
        return await StartAsync(
            new RunRequest
            {
                Agent = original.AgentId.ToString(),
                Tags = original.Tags,
                EndpointUrl = request?.EndpointUrl ?? original.EndpointUrl,
                Group = request?.Group,
                Note = request?.Note,
            },
            original.Placement,
            agents,
            scenarios,
            checks,
            runs,
            client,
            judge,
            simulator,
            cancellationToken
        );
    }

    /// <summary>What a re-check of the run would do and whether it can start (read-only). 404 for an unknown run.</summary>
    private static async Task<IResult> RecheckPlanAsync(
        Guid id,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        Judge judge,
        CancellationToken cancellationToken
    )
    {
        var planned = await PlanRecheckAsync(id, agents, scenarios, checks, runs, cancellationToken);
        return planned is null
            ? ProblemResults.NotFound("Run", id.ToString())
            : Results.Ok(planned.Plan with { JudgeModel = judge.ModelName });
    }

    /// <summary>
    /// Queues a re-check: the stored answers of run <paramref name="id"/> (of the run that called the agent, when that is itself a
    /// re-check) judged again by the current checks, without calling the agent. 404 for an unknown run; 409 with the reason (and the
    /// mismatches) when the plan says it cannot start.
    /// </summary>
    private static async Task<IResult> RecheckAsync(
        Guid id,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var planned = await PlanRecheckAsync(id, agents, scenarios, checks, runs, cancellationToken);
        if (planned is null)
        {
            return ProblemResults.NotFound("Run", id.ToString());
        }
        if (!planned.Plan.Possible)
        {
            return Results.Problem(
                title: "The run cannot be re-checked",
                detail: string.Join("; ", [planned.Plan.Reason, .. planned.Plan.Mismatches]),
                statusCode: StatusCodes.Status409Conflict,
                extensions: planned.Plan.Mismatches.Count > 0
                    ? new Dictionary<string, object?> { ["mismatches"] = planned.Plan.Mismatches }
                    : null
            );
        }
        var source = planned.Original;
        if (await agents.GetAsync(source.AgentId.ToString(), cancellationToken) is { Evaluation: false } off)
        {
            return ProblemResults.FeatureOff(off.Name, "evaluation");
        }
        var created = await runs.EnqueueAsync(
            source.AgentId,
            source.AgentVersionId,
            source.Tags,
            source.EndpointUrl,
            source.Id,
            planned.Shown.Id,
            source.Placement,
            label: source.Label,
            dataVersion: source.DataVersion,
            dataDescription: source.DataDescription,
            cancellationToken: cancellationToken
        );
        return Results.Accepted(
            $"/eval/runs/{created}",
            new RunCreated
            {
                RunId = created,
                AgentVersionId = source.AgentVersionId,
                VersionCreated = false,
            }
        );
    }

    private sealed record PlannedRecheck(RunRow Shown, RunRow Original, RecheckPlan Plan);

    private static async Task<PlannedRecheck?> PlanRecheckAsync(
        Guid id,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var shown = await runs.GetAsync(id, cancellationToken);
        if (shown is null)
        {
            return null;
        }
        // A re-check has the answers of the run it was made from: that one is what is re-checked again.
        var original = shown.RecheckOf is { } originalId ? await runs.GetAsync(originalId, cancellationToken) ?? shown : shown;
        var version = await agents.GetVersionAsync(original.AgentVersionId, cancellationToken);
        var originalSamples = await runs.ListScenarioResultsAsync(original.Id, cancellationToken);
        var shownSamples = original.Id == shown.Id ? originalSamples : await runs.ListScenarioResultsAsync(shown.Id, cancellationToken);
        var plan = RecheckPlanner.Of(
            shown.State,
            originalSamples,
            shownSamples,
            await runs.ListCheckResultsAsync(shown.Id, cancellationToken),
            await scenarios.ListAsync(original.AgentId, cancellationToken: cancellationToken),
            await checks.ListAsync(original.AgentId, cancellationToken: cancellationToken),
            ContractContent.Read(version?.Content ?? throw new InvalidOperationException("The run's version is gone."))
        );
        return new PlannedRecheck(shown, original, plan);
    }

    /// <summary>
    /// A page of runs, newest first, with how many runs match. <c>page</c> (1-based, default 1; past the last page gives the last one),
    /// <c>pageSize</c> (default 20, 1 to 100), <c>version</c> (repeatable agent version ids: only their runs), <c>group</c> (only the runs started in that group), <c>grouping</c> (<c>None</c>: only runs in no group,
    /// <c>Any</c>: only runs in some group; a re-check counts as in the group of the run it was made from; default <c>All</c>). 404: no such agent; 422: a bad page or page size,
    /// or both a group and a grouping other than <c>All</c>. <c>tag</c> (repeatable): only the runs that ran the scenarios of any of these tags, i.e. limited
    /// to tags including one of them, or not limited at all. <c>dataVersion</c> (repeatable): only the runs on one of these retrieval data versions.
    /// </summary>
    private static async Task<IResult> ListAsync(
        [FromQuery] string? agent,
        [FromQuery] int? page,
        [FromQuery] Guid[]? version,
        [FromQuery] int? pageSize,
        [FromQuery] string? group,
        [FromQuery] RunGrouping? grouping,
        [FromQuery] string[]? tag,
        [FromQuery] string[]? dataVersion,
        AgentStore agents,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrEmpty(group) && grouping is RunGrouping.None or RunGrouping.Any)
        {
            return ProblemResults.Invalid("grouping", "Name a group or a grouping (none, any), not both.");
        }
        var size = pageSize ?? DefaultPageSize;
        if (size is < 1 or > MaxPageSize)
        {
            return ProblemResults.Invalid("pageSize", $"The page size is between 1 and {MaxPageSize}.");
        }
        if (page is < 1)
        {
            return ProblemResults.Invalid("page", "The page is 1 or more.");
        }
        Guid? agentId = null;
        if (agent is not null)
        {
            var found = await agents.GetAsync(agent, cancellationToken);
            if (found is null)
            {
                return ProblemResults.NotFound("Agent", agent);
            }
            agentId = found.Id;
        }
        var runGroup = string.IsNullOrEmpty(group) ? null : group;
        var runGrouping = grouping ?? RunGrouping.All;
        var totalCount = await runs.CountAsync(
            agentId,
            version ?? [],
            runGroup,
            runGrouping,
            tag ?? [],
            dataVersion ?? [],
            cancellationToken
        );
        var shownPage = Math.Min(page ?? 1, Math.Max(1, (totalCount + size - 1) / size));
        var rows = await runs.ListPageAsync(
            agentId,
            version ?? [],
            (shownPage - 1) * size,
            size,
            runGroup,
            runGrouping,
            tag ?? [],
            dataVersion ?? [],
            cancellationToken
        );
        return Results.Ok(
            new RunList
            {
                Runs = [.. rows.Select(RunReport.Summary)],
                Page = shownPage,
                TotalCount = totalCount,
            }
        );
    }

    private static async Task<IResult> GetAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        return run is null ? ProblemResults.NotFound("Run", id.ToString()) : Results.Ok(RunReport.Summary(run));
    }

    /// <summary>
    /// The results so far — a run that is still going has some, a queued one none. Once the run is done every check also carries its
    /// <see cref="CheckRunResult.Regression"/> state against its history, and the results the summary of them — except a run started in a
    /// group (or a re-check of one): an experiment has no regression states.
    /// </summary>
    private static async Task<IResult> ResultsAsync(Guid id, RunStore runs, AgentStore agents, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null)
        {
            return ProblemResults.NotFound("Run", id.ToString());
        }
        var samples = await runs.ListScenarioResultsAsync(id, cancellationToken);
        var verdicts = await runs.ListCheckResultsAsync(id, cancellationToken);
        var results = RunReport.Results(run, samples, verdicts);
        if (run.State != RunStatus.Done)
        {
            return Results.Ok(results);
        }
        var original = run.RecheckOf is { } recheckOf ? await runs.GetAsync(recheckOf, cancellationToken) : run;
        if ((original ?? run).RunGroup is not null)
        {
            return Results.Ok(results);
        }
        var n = RegressionReport.HistoryOf((await agents.GetAsync(run.AgentId.ToString(), cancellationToken))?.Regression);
        // A re-check stands where the run it was made from stood: the history is what came before that run.
        var history = await runs.ListCheckHistoriesAsync(
            id,
            run.RecheckOf ?? id,
            run.ComparedWith,
            RegressionReport.RowsNeeded(n),
            cancellationToken
        );
        return Results.Ok(RegressionReport.Apply(results, samples, verdicts, history, n));
    }

    /// <summary>202 for any known run: a queued one is cancelled at once, a running one before its next sample, a finished one stays.</summary>
    private static async Task<IResult> CancelAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        if (await runs.GetAsync(id, cancellationToken) is null)
        {
            return ProblemResults.NotFound("Run", id.ToString());
        }
        await runs.RequestCancelAsync(id, cancellationToken);
        return Results.Accepted($"/eval/runs/{id}");
    }

    /// <summary>204 once the note of run <paramref name="id"/> is set (none or blank clears it), in any status. 404: no such run; 422: a note that is too long.</summary>
    private static async Task<IResult> SetNoteAsync(Guid id, NoteRequest request, RunStore runs, CancellationToken cancellationToken)
    {
        if (NoteRules.Problem(request.Note) is { } problem)
        {
            return ProblemResults.Invalid("note", problem);
        }
        return await runs.SetNoteAsync(id, NoteRules.Normalize(request.Note), cancellationToken)
            ? Results.NoContent()
            : ProblemResults.NotFound("Run", id.ToString());
    }
}
