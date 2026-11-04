using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Evaluation.Runs.AgentErrors;

internal static class AgentErrorEndpoints
{
    public static IEndpointRouteBuilder MapAgentErrors(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/agent-errors", ListAsync).Produces<IReadOnlyList<AgentError>>();
        return app;
    }

    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] Guid[]? version,
        [FromQuery] string[]? dataVersion,
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
        var picked = await RecentRuns.PickAsync(found.Id, considered, dataVersion ?? [], runs, cancellationToken);
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
                    // Why: only rows with at least one agent error are listed, so the newest error run exists
                    NewestErrorRunId = row.NewestErrorRunId!.Value,
                    NewestError = row.NewestError ?? "",
                })
                .ToList()
        );
    }
}
