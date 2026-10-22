using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;
using Suwageval.Server.Problems;
using Suwageval.Server.Scenarios;
using Suwageval.Server.Simulation;

namespace Suwageval.Server.Runs;

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
        app.MapGet("/agents/{agent}/pass-rates", PassRatesAsync).Produces<IReadOnlyList<RunPassRates>>();
        app.MapGet("/agents/{agent}/unstable-checks", UnstableChecksAsync).Produces<IReadOnlyList<UnstableCheck>>();
        app.MapGet("/agents/{agent}/agent-errors", AgentErrorsAsync).Produces<IReadOnlyList<AgentError>>();
        return app;
    }

    /// <summary>The unstable checks and the agent errors look at the newest this many finished runs of the agent.</summary>
    internal const int UnstableCheckRuns = 200;

    /// <summary>
    /// The open unstable checks of an agent (<see cref="UnstableChecks"/>: unstable in the newest considered version that ran them at least
    /// twice), over its newest <see cref="UnstableCheckRuns"/> finished runs of the considered versions, each with the verdicts it shows
    /// (<see cref="EffectiveRuns"/>). The considered versions are the <c>version</c> query (repeatable agent version ids), else every
    /// version not archived. 404: no such agent.
    /// </summary>
    private static async Task<IResult> UnstableChecksAsync(
        string agent,
        [FromQuery] Guid[]? version,
        AgentStore agents,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var versions = await agents.ListVersionsAsync(found.Id, cancellationToken);
        var considered = Considered(versions, version);
        var rows = await runs.ListRunCheckVerdictsAsync(await PickAsync(found.Id, considered, runs, cancellationToken), cancellationToken);
        var labels = versions.ToDictionary(row => row.Id, row => row.Label);
        return Results.Ok(
            UnstableChecks
                .Find(rows, considered)
                .Select(check => check with { VersionLabel = labels.GetValueOrDefault(check.AgentVersionId) })
                .ToList()
        );
    }

    /// <summary>The versions the open problems are decided over, newest first: the requested ones, else every one not archived.</summary>
    private static List<Guid> Considered(IReadOnlyList<AgentVersionRow> versionsNewestFirst, Guid[]? requested) =>
        [
            .. versionsNewestFirst
                .Where(row => requested is { Length: > 0 } ? requested.Contains(row.Id) : row.ArchivedAt is null)
                .Select(row => row.Id),
        ];

    private static async Task<IReadOnlyList<EffectiveRuns.Point>> PickAsync(
        Guid agentId,
        IReadOnlyCollection<Guid> considered,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var newestFirst = (await runs.ListAsync(agentId, cancellationToken)).Where(run => considered.Contains(run.AgentVersionId));
        return EffectiveRuns.Pick([.. newestFirst], UnstableCheckRuns);
    }

    /// <summary>
    /// Where the agent gave no answer and no newer version has run the scenario without it (<see cref="OpenAgentErrors"/>): per agent
    /// version and scenario, the errors over the samples asked, the newest run and error text, worst first. The same runs and considered
    /// versions as the unstable checks (a re-check does not call the agent, so it is not counted). 404: no such agent.
    /// </summary>
    private static async Task<IResult> AgentErrorsAsync(
        string agent,
        [FromQuery] Guid[]? version,
        AgentStore agents,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var versions = await agents.ListVersionsAsync(found.Id, cancellationToken);
        var considered = Considered(versions, version);
        var picked = await PickAsync(found.Id, considered, runs, cancellationToken);
        var rows = await runs.ListAgentErrorsAsync([.. picked.Select(point => point.Run.Id)], cancellationToken);
        var labels = versions.ToDictionary(row => row.Id, row => row.Label);
        return Results.Ok(
            OpenAgentErrors
                .Find(rows, considered)
                .Select(row => new AgentError
                {
                    ScenarioKey = row.ScenarioKey,
                    AgentVersionId = row.AgentVersionId,
                    VersionLabel = labels.GetValueOrDefault(row.AgentVersionId),
                    Errors = row.Errors,
                    Samples = row.Samples,
                    NewestErrorRunId = row.NewestErrorRunId!.Value,
                    NewestError = row.NewestError ?? "",
                })
                .ToList()
        );
    }

    /// <summary>
    /// The pass-rate trend of an agent: its finished runs that called the agent (the newest <see cref="PassRateTrend.MaxRuns"/>, oldest first) with
    /// the rate over all checks and per tag and the turn time (median, p95) over the same samples. A run has a rate for a tag only when it ran every scenario of the tag (<see cref="TagCoverage"/>); a run with no check is left out. 404: no such agent.
    /// </summary>
    private static async Task<IResult> PassRatesAsync(
        string agent,
        AgentStore agents,
        ScenarioStore scenarios,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var picked = EffectiveRuns.Pick(await runs.ListAsync(found.Id, cancellationToken), PassRateTrend.MaxRuns);
        var versions = (await agents.ListVersionsAsync(found.Id, cancellationToken)).ToDictionary(version => version.Id);
        var lifetimes = await scenarios.ListLifetimesAsync(found.Id, cancellationToken);
        var tagsOf = lifetimes.ToDictionary(scenario => scenario.Key, scenario => (IReadOnlyList<string>)scenario.Tags);
        var shownIds = picked.Select(point => point.Shown.Id).ToList();
        var checksOf = (await runs.ListTrendChecksAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
        var durationsOf = (await runs.ListTrendDurationsAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
        var ranOf = (await runs.ListTrendScenariosAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
        var points = new List<RunPassRates>();
        foreach (var (run, recheck) in picked)
        {
            var shown = recheck ?? run;
            if (
                PassRateTrend.Rates(
                    [
                        .. checksOf[shown.Id]
                            .Select(row => (row.ScenarioKey, RunReport.Reaches(row.SamplesPassed, row.Samples, row.PassThreshold))),
                    ],
                    [.. durationsOf[shown.Id].Select(row => (row.ScenarioKey, row.DurationMs))],
                    tagsOf,
                    TagCoverage.CompleteTags(run.CreatedAt, [.. ranOf[shown.Id].Select(row => row.ScenarioKey)], lifetimes)
                )
                is not { } rates
            )
            {
                continue;
            }
            var version = versions[run.AgentVersionId];
            points.Add(
                new RunPassRates
                {
                    RunId = run.Id,
                    CreatedAt = run.CreatedAt,
                    RecheckedBy = recheck?.Id,
                    AgentVersionId = version.Id,
                    LimitedToTags = run.Tags,
                    VersionLabel = version.Label,
                    ModelNames = ContractContent.ModelNames(ContractContent.Read(version.Content)),
                    PassRate = rates.PassRate,
                    MedianMs = rates.MedianMs,
                    P95Ms = rates.P95Ms,
                    Tags = rates.Tags,
                }
            );
        }
        return Results.Ok(points);
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

        var agent = await agents.GetAsync(request.Agent, cancellationToken);
        if (agent is null)
        {
            return ProblemResults.NotFound("Agent", request.Agent);
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
            ContractContent.Json(contract),
            contract.Label,
            contract.Context.Retrieval?.Description,
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
                detail: "The scenarios have judge checks or $judge argument matching, but the judge's model is not configured (Models:<Judge:Model>:BaseUrl and :Model); nothing was queued.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        if (!simulator.IsConfigured && picked.Any(scenario => StoredContent.Scenario(scenario.Content).Simulated))
        {
            return Results.Problem(
                title: "No simulator model is configured",
                detail: "The scenarios have a simulated user, but the simulator's model is not configured (Models:simulator, or else Models:judge: :BaseUrl and :Model); nothing was queued.",
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
            cancellationToken: cancellationToken
        );
        return Results.Accepted(
            $"/runs/{id}",
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
        var created = await runs.EnqueueAsync(
            source.AgentId,
            source.AgentVersionId,
            source.Tags,
            source.EndpointUrl,
            source.Id,
            planned.Shown.Id,
            source.Placement,
            label: source.Label,
            cancellationToken: cancellationToken
        );
        return Results.Accepted(
            $"/runs/{created}",
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
    /// or both a group and a grouping other than <c>All</c>.
    /// </summary>
    private static async Task<IResult> ListAsync(
        [FromQuery] string? agent,
        [FromQuery] int? page,
        [FromQuery] Guid[]? version,
        [FromQuery] int? pageSize,
        [FromQuery] string? group,
        [FromQuery] RunGrouping? grouping,
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
        var totalCount = await runs.CountAsync(agentId, version ?? [], runGroup, runGrouping, cancellationToken);
        var shownPage = Math.Min(page ?? 1, Math.Max(1, (totalCount + size - 1) / size));
        var rows = await runs.ListPageAsync(agentId, version ?? [], (shownPage - 1) * size, size, runGroup, runGrouping, cancellationToken);
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
        return Results.Accepted($"/runs/{id}");
    }
}
