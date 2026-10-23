using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Runs.AgentErrors;

/// <summary>The route of the open agent errors of an agent.</summary>
internal static class AgentErrorEndpoints
{
    public static IEndpointRouteBuilder MapAgentErrors(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/agent-errors", ListAsync).Produces<IReadOnlyList<AgentError>>();
        return app;
    }

    /// <summary>
    /// Where the agent gave no answer and no newer version has run the scenario without it (<see cref="OpenAgentErrors"/>): per agent
    /// version and scenario, the errors over the samples asked, the newest run and error text, worst first. The same runs and considered
    /// versions as the unstable checks (a re-check does not call the agent, so it is not counted). 404: no such agent.
    /// </summary>
    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] Guid[]? version,
        AgentStore agents,
        RunStore runs,
        AgentErrorStore agentErrors,
        CancellationToken cancellationToken
    )
    {
        var found = await agents.GetAsync(agent, cancellationToken);
        if (found is null)
        {
            return ProblemResults.NotFound("Agent", agent);
        }
        var versions = await agents.ListVersionsAsync(found.Id, cancellationToken);
        var considered = RecentRuns.Considered(versions, version);
        var picked = await RecentRuns.PickAsync(found.Id, considered, runs, cancellationToken);
        var rows = await agentErrors.ListAgentErrorsAsync([.. picked.Select(point => point.Run.Id)], cancellationToken);
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
}
