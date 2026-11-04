using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Evaluation.Runs.Unstable;

internal static class UnstableEndpoints
{
    public static IEndpointRouteBuilder MapUnstable(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/unstable-checks", ListAsync).Produces<IReadOnlyList<UnstableCheck>>();
        return app;
    }

    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] Guid[]? version,
        [FromQuery] string[]? dataVersion,
        AgentStore agents,
        RunStore runs,
        UnstableStore unstable,
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
        var rows = await unstable.ListRunCheckVerdictsAsync(
            await RecentRuns.PickAsync(found.Id, considered, dataVersion ?? [], runs, cancellationToken),
            cancellationToken
        );
        var labels = versions.ToDictionary(row => row.Id, row => row.Label);
        return Results.Ok(
            UnstableChecks
                .Find(rows, considered)
                .Select(check => check with { VersionLabel = labels.GetValueOrDefault(check.AgentVersionId) })
                .ToList()
        );
    }
}
