using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Runs.Unstable;

/// <summary>The route of the open unstable checks of an agent.</summary>
internal static class UnstableEndpoints
{
    public static IEndpointRouteBuilder MapUnstable(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/unstable-checks", ListAsync).Produces<IReadOnlyList<UnstableCheck>>();
        return app;
    }

    /// <summary>
    /// The open unstable checks of an agent (<see cref="UnstableChecks"/>: unstable in the newest considered version that ran them at least
    /// twice), over its newest <see cref="RecentRuns.MaxRuns"/> finished runs of the considered versions, each with the verdicts it shows
    /// (<see cref="EffectiveRuns"/>). The considered versions are the <c>version</c> query (repeatable agent version ids), else every
    /// version not archived. 404: no such agent.
    /// </summary>
    private static async Task<IResult> ListAsync(
        string agent,
        [FromQuery] Guid[]? version,
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
            await RecentRuns.PickAsync(found.Id, considered, runs, cancellationToken),
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
