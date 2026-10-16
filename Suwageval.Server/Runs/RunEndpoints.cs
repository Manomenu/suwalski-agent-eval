using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Checks;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>The routes of runs: start one, follow it, read its results, cancel it.</summary>
internal static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRuns(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/runs", CreateAsync).Produces<RunCreated>(StatusCodes.Status202Accepted);
        app.MapGet("/runs", ListAsync).Produces<IReadOnlyList<RunSummary>>();
        app.MapGet("/runs/{id:guid}", GetAsync).Produces<RunSummary>();
        app.MapGet("/runs/{id:guid}/results", ResultsAsync).Produces<RunResults>();
        app.MapPost("/runs/{id:guid}/cancel", CancelAsync).Produces(StatusCodes.Status202Accepted);
        return app;
    }

    /// <summary>
    /// 202 with the run's id once it is queued. 404: no such agent or version; 422: the request or what it selects is not
    /// runnable (a bad endpoint, no scenario with the tag); 409: the version does not fit the scenarios and checks — the
    /// mismatches are listed and nothing is queued.
    /// </summary>
    private static async Task<IResult> CreateAsync(
        RunRequest request,
        AgentStore agents,
        ScenarioStore scenarios,
        CheckStore checks,
        RunStore runs,
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
        var version = request.AgentVersionId is { } versionId
            ? await agents.GetVersionAsync(versionId, cancellationToken)
            : await NewestVersionAsync(agents, agent.Id, cancellationToken);
        if (version is null || version.AgentId != agent.Id)
        {
            return NotFound(
                request.AgentVersionId is null
                    ? $"Agent '{agent.Name}' has no version yet: push it first."
                    : $"Agent '{agent.Name}' has no version {request.AgentVersionId}."
            );
        }

        var picked = await scenarios.ListAsync(agent.Id, request.Tag, cancellationToken);
        if (picked.Count == 0)
        {
            return Invalid("tag", request.Tag is null ? "The agent has no scenarios." : $"No scenario has the tag '{request.Tag}'.");
        }
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

        var id = await runs.EnqueueAsync(agent.Id, version.Id, request.Tag, request.EndpointUrl ?? agent.EndpointUrl, cancellationToken);
        return Results.Accepted($"/runs/{id}", new RunCreated { RunId = id });
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string? agent,
        AgentStore agents,
        RunStore runs,
        CancellationToken cancellationToken
    )
    {
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
        var rows = await runs.ListAsync(agentId, cancellationToken);
        return Results.Ok(rows.Select(RunReport.Summary).ToList());
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

    private static async Task<AgentVersionRow?> NewestVersionAsync(AgentStore agents, Guid agentId, CancellationToken cancellationToken)
    {
        var versions = await agents.ListVersionsAsync(agentId, cancellationToken);
        return versions.Count > 0 ? versions[0] : null;
    }

    private static IResult NotFound(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not found");

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]> { [field] = [message] },
            statusCode: StatusCodes.Status422UnprocessableEntity
        );
}
