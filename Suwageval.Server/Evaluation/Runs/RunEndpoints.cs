using Microsoft.AspNetCore.Mvc;
using Suwageval.AgentSdk.Agents;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Evaluation.Invocation;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Evaluation.Simulation;
using Suwageval.Server.Judges;
using Suwageval.Server.Problems;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

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
        app.MapGet("/runs/{id:guid}/recheck", RecheckPlanAsync).Produces<RecheckPlan>();
        app.MapPost("/runs/{id:guid}/recheck", RecheckAsync).Produces<RunCreated>(StatusCodes.Status202Accepted);
        app.MapPost("/runs/{id:guid}/cancel", CancelAsync).Produces(StatusCodes.Status202Accepted);
        app.MapPut("/runs/{id:guid}/note", SetNoteAsync).Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static async Task<IResult> CreateAsync(
        RunRequest request,
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

        if (request.Gate && (request.Tags.Count > 0 || request.Group is not null))
        {
            return ProblemResults.Invalid(
                request.Tags.Count > 0 ? "tags" : "group",
                "A gate run takes every scenario with an acGuardrail: no tags and no group."
            );
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
        if (request.Gate)
        {
            picked = [.. picked.Where(scenario => StoredContent.Scenario(scenario.Content).AcGuardrail is not null)];
            if (await GateProblemAsync(picked, agent.Id, checks, cancellationToken) is { } gateProblem)
            {
                return gateProblem;
            }
        }
        else if (picked.Count == 0)
        {
            return ProblemResults.Invalid(
                "tags",
                request.Tags.Count == 0
                    ? "The agent has no scenarios."
                    : $"No scenario has any of the tags {string.Join(", ", request.Tags.Select(tag => $"'{tag}'"))}."
            );
        }

        // Why: the version is what the agent says it is right now, so its contract is read and registered first and nothing is queued when the agent cannot say
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

        var placement = RunPlacement.Decide(request.Origin, agent.Regression);
        if (request.Group is not null || request.Gate)
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
            gateRetryLimit: request.Gate ? AcGuardrailRules.EffectiveRetryLimit(null, agent.AcGuardrailRetryLimit) : null,
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

    private static async Task<IResult?> GateProblemAsync(
        IReadOnlyList<ScenarioRow> guardrails,
        Guid agentId,
        CheckStore checks,
        CancellationToken cancellationToken
    )
    {
        if (guardrails.Count == 0)
        {
            return ProblemResults.Invalid("gate", "The agent has no scenario with an acGuardrail: there is nothing for the gate to run.");
        }
        var withChecks = (await checks.ListAsync(agentId, cancellationToken: cancellationToken))
            .Select(check => check.ScenarioKey)
            .ToHashSet();
        if (guardrails.FirstOrDefault(scenario => !withChecks.Contains(scenario.Key)) is { } withoutChecks)
        {
            return ProblemResults.Invalid("gate", $"The scenario '{withoutChecks.Key}': {AcGuardrailRules.NoChecks}");
        }
        var duplicate = guardrails
            // Why: only scenarios with an acGuardrail are grouped, so it is set
            .GroupBy(scenario => StoredContent.Scenario(scenario.Content).AcGuardrail!.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        return duplicate is null
            ? null
            : ProblemResults.Invalid(
                "gate",
                $"The acceptance criterion '{duplicate.Key}' is guarded by several scenarios on the server ({string.Join(", ", duplicate.Select(scenario => $"'{scenario.Key}'"))}); one scenario guards one criterion."
            );
    }

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
        // Why: a re-check has the answers of the run it was made from, so that run is what is re-checked again
        var original = shown.RecheckOf is { } originalId ? await runs.GetAsync(originalId, cancellationToken) ?? shown : shown;
        if (original.Gate)
        {
            return new PlannedRecheck(shown, original, RecheckPlanner.GateRun());
        }
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
        if ((original ?? run).RunGroup is not null || run.Gate)
        {
            return Results.Ok(results);
        }
        var n = RegressionReport.HistoryOf((await agents.GetAsync(run.AgentId.ToString(), cancellationToken))?.Regression);
        // Why: a re-check stands where its original stood, so the history is what came before that run
        var history = await runs.ListCheckHistoriesAsync(
            id,
            run.RecheckOf ?? id,
            run.ComparedWith,
            RegressionReport.RowsNeeded(n),
            cancellationToken
        );
        return Results.Ok(RegressionReport.Apply(results, samples, verdicts, history, n));
    }

    private static async Task<IResult> CancelAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        if (await runs.GetAsync(id, cancellationToken) is null)
        {
            return ProblemResults.NotFound("Run", id.ToString());
        }
        await runs.RequestCancelAsync(id, cancellationToken);
        return Results.Accepted($"/eval/runs/{id}");
    }

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
