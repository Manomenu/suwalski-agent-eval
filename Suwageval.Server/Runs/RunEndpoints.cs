using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Invocation;
using Suwageval.Server.Judges;
using Suwageval.Server.Scenarios;

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
        app.MapPost("/runs/{id:guid}/cancel", CancelAsync).Produces(StatusCodes.Status202Accepted);
        app.MapGet("/agents/{agent}/pass-rates", PassRatesAsync).Produces<IReadOnlyList<RunPassRates>>();
        return app;
    }

    /// <summary>
    /// The pass-rate trend of an agent: its finished runs (the newest <see cref="PassRateTrend.MaxRuns"/>, oldest first) with
    /// the rate over all checks and per tag. Tags are the scenarios' current ones; a run with no check is left out. 404: no such agent.
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
            return NotFound($"There is no agent '{agent}'.");
        }
        var picked = PassRateTrend.Pick(await runs.ListAsync(found.Id, cancellationToken));
        var versions = (await agents.ListVersionsAsync(found.Id, cancellationToken)).ToDictionary(version => version.Id);
        var tagsOf = (await scenarios.ListAsync(found.Id, cancellationToken: cancellationToken)).ToDictionary(
            scenario => scenario.Key,
            scenario => (IReadOnlyList<string>)scenario.Tags
        );
        var points = new List<RunPassRates>();
        foreach (var run in picked)
        {
            var results = RunReport.Results(
                run,
                await runs.ListScenarioResultsAsync(run.Id, cancellationToken),
                await runs.ListCheckResultsAsync(run.Id, cancellationToken)
            );
            if (PassRateTrend.Rates(results, tagsOf) is not { } rates)
            {
                continue;
            }
            var version = versions[run.AgentVersionId];
            points.Add(
                new RunPassRates
                {
                    RunId = run.Id,
                    CreatedAt = run.CreatedAt,
                    AgentVersionId = version.Id,
                    Tag = run.Tag,
                    VersionLabel = version.Label,
                    ModelNames = ContractContent.ModelNames(StoredContent.Version(version.Content)),
                    PassRate = rates.PassRate,
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
    /// picked and no judge model is configured.
    /// </summary>
    private static async Task<IResult> CreateAsync(
        RunRequest request,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
        AgentClient client,
        Judge judge,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Agent))
        {
            return Invalid("agent", "The agent's name or id is required.");
        }
        if (request.EndpointUrl is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"))
        {
            return Invalid("endpointUrl", "The endpoint is an absolute http or https URL.");
        }

        var agent = await agents.GetAsync(request.Agent, cancellationToken);
        if (agent is null)
        {
            return NotFound($"There is no agent '{request.Agent}'.");
        }

        var picked = await scenarios.ListAsync(agent.Id, request.Tag, cancellationToken);
        if (picked.Count == 0)
        {
            return Invalid("tag", request.Tag is null ? "The agent has no scenarios." : $"No scenario has the tag '{request.Tag}'.");
        }

        // The version is what the agent says it is right now: read its contract, register it (the same content is the same
        // version), and run against that. Nothing is queued when the agent cannot say.
        var endpoint = request.EndpointUrl ?? agent.EndpointUrl;
        var served = await client.GetContractAsync(new Uri(endpoint), cancellationToken);
        if (served.Contract is not { } contract)
        {
            return BadGateway("The agent's contract could not be read", served.Error ?? "The agent served nothing.");
        }
        var contractProblems = ContractContent.Problems(contract);
        if (contractProblems.Count > 0)
        {
            return BadGateway(
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
        var pickedChecks = (await checks.ListAsync(agent.Id, cancellationToken: cancellationToken)).Where(check =>
            keys.Contains(check.ScenarioKey)
        );

        var mismatches = RunCompatibility.Mismatches(
            StoredContent.Version(version.Content),
            [.. picked.Select(scenario => (scenario.Key, StoredContent.Scenario(scenario.Content)))],
            [.. pickedChecks.Select(StoredContent.Check)]
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

        if (
            !judge.IsConfigured
            && pickedChecks.Any(check => StoredContent.Check(check) is JudgeCheck || CheckEvaluator.UsesJudge(StoredContent.Check(check)))
        )
        {
            return Results.Problem(
                title: "No judge model is configured",
                detail: "The scenarios have judge checks or $judge argument matching, but the judge's model is not configured (Models:<Judge:Model>:BaseUrl and :Model); nothing was queued.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        var id = await runs.EnqueueAsync(agent.Id, version.Id, request.Tag, endpoint, cancellationToken);
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
    /// A new run with the parameters of run <paramref name="id"/> — its agent, tag and endpoint (unless the body gives another) —
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
        CancellationToken cancellationToken
    )
    {
        var original = await runs.GetAsync(id, cancellationToken);
        if (original is null)
        {
            return NotFound($"There is no run {id}.");
        }
        return await CreateAsync(
            new RunRequest
            {
                Agent = original.AgentId.ToString(),
                Tag = original.Tag,
                EndpointUrl = request?.EndpointUrl ?? original.EndpointUrl,
            },
            agents,
            scenarios,
            checks,
            runs,
            client,
            judge,
            cancellationToken
        );
    }

    /// <summary>
    /// A page of runs, newest first, with the id to continue from. <c>limit</c> (default 20, 1 to 100), <c>before</c> (a run id: only
    /// older runs), <c>version</c> (repeatable agent version ids: only their runs). 404: no such agent; 422: a bad limit or an unknown
    /// <c>before</c>.
    /// </summary>
    private static async Task<IResult> ListAsync(
        [FromQuery] string? agent,
        [FromQuery] Guid? before,
        [FromQuery] Guid[]? version,
        [FromQuery] int? limit,
        AgentStore agents,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
        var size = limit ?? DefaultPageSize;
        if (size is < 1 or > MaxPageSize)
        {
            return Invalid("limit", $"The limit is between 1 and {MaxPageSize}.");
        }
        Guid? agentId = null;
        if (agent is not null)
        {
            var found = await agents.GetAsync(agent, cancellationToken);
            if (found is null)
            {
                return NotFound($"There is no agent '{agent}'.");
            }
            agentId = found.Id;
        }
        if (before is { } cursor && await runs.GetAsync(cursor, cancellationToken) is null)
        {
            return Invalid("before", $"There is no run {cursor}.");
        }
        // One more than asked for tells whether an older run exists.
        var rows = await runs.ListPageAsync(agentId, before, version ?? [], size + 1, cancellationToken);
        var page = rows.Take(size).Select(RunReport.Summary).ToList();
        return Results.Ok(new RunList { Runs = page, Next = rows.Count > size ? page[^1].Id : null });
    }

    private static async Task<IResult> GetAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        return run is null ? NotFound($"There is no run {id}.") : Results.Ok(RunReport.Summary(run));
    }

    /// <summary>The results so far — a run that is still going has some, a queued one none.</summary>
    private static async Task<IResult> ResultsAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null)
        {
            return NotFound($"There is no run {id}.");
        }
        var samples = await runs.ListScenarioResultsAsync(id, cancellationToken);
        var verdicts = await runs.ListCheckResultsAsync(id, cancellationToken);
        return Results.Ok(RunReport.Results(run, samples, verdicts));
    }

    /// <summary>202 for any known run: a queued one is cancelled at once, a running one before its next sample, a finished one stays.</summary>
    private static async Task<IResult> CancelAsync(Guid id, RunStore runs, CancellationToken cancellationToken)
    {
        if (await runs.GetAsync(id, cancellationToken) is null)
        {
            return NotFound($"There is no run {id}.");
        }
        await runs.RequestCancelAsync(id, cancellationToken);
        return Results.Accepted($"/runs/{id}");
    }

    private static IResult BadGateway(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status502BadGateway);

    private static IResult NotFound(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not found");

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]> { [field] = [message] },
            statusCode: StatusCodes.Status422UnprocessableEntity
        );
}
