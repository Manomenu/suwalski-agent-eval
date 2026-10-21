using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>The route of the variants view: runs of one agent side by side, picked one by one or by their group.</summary>
internal static class VariantEndpoints
{
    public static IEndpointRouteBuilder MapVariants(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/runs/variants", VariantsAsync).Produces<RunVariants>();
        return app;
    }

    /// <summary>
    /// The runs of one agent as variants (<see cref="RunVariantsReport"/>): either <c>runs</c> (repeatable run ids, each once, in that
    /// order) or <c>group</c> (every run started in that group, oldest first; <c>agent</c> names the agent when the group has runs of
    /// several). 404: no such run, group or agent; 422: both or neither of runs and group, more than <see cref="RunVariantsReport.MaxRuns"/>
    /// runs, runs of different agents, a bad group.
    /// </summary>
    private static async Task<IResult> VariantsAsync(
        [FromQuery] Guid[]? runs,
        [FromQuery] string? group,
        [FromQuery] string? agent,
        AgentStore agents,
        RunStore store,
        ScenarioStore scenarios,
        CancellationToken cancellationToken
    )
    {
        var ids = (runs ?? []).Distinct().ToList();
        if ((ids.Count > 0) == !string.IsNullOrEmpty(group))
        {
            return ProblemResults.Invalid(
                "runs",
                "Give the runs to show (runs) or the group they are in (group), not both and not neither."
            );
        }
        var rows = new List<RunRow>();
        if (ids.Count > 0)
        {
            if (ids.Count > RunVariantsReport.MaxRuns)
            {
                return ProblemResults.Invalid("runs", $"Choose at most {RunVariantsReport.MaxRuns} runs.");
            }
            foreach (var id in ids)
            {
                var row = await store.GetAsync(id, cancellationToken);
                if (row is null)
                {
                    return ProblemResults.NotFound("Run", id.ToString());
                }
                rows.Add(row);
            }
            group = null;
        }
        else
        {
            if (RunGroupRules.Problem(group) is { } problem)
            {
                return ProblemResults.Invalid("group", problem);
            }
            Guid? agentId = null;
            if (!string.IsNullOrEmpty(agent))
            {
                var found = await agents.GetAsync(agent, cancellationToken);
                if (found is null)
                {
                    return ProblemResults.NotFound("Agent", agent);
                }
                agentId = found.Id;
            }
            var newestFirst = await store.ListPageAsync(agentId, null, [], RunVariantsReport.MaxRuns + 1, group, cancellationToken);
            if (newestFirst.Count == 0)
            {
                return ProblemResults.NotFound("Run group", group!);
            }
            if (newestFirst.Count > RunVariantsReport.MaxRuns)
            {
                return ProblemResults.Invalid("group", $"The group has more than {RunVariantsReport.MaxRuns} runs; name the runs to show.");
            }
            rows = [.. newestFirst.Reverse()];
        }
        var agentNames = rows.Select(row => row.AgentName).Distinct().ToList();
        if (agentNames.Count > 1)
        {
            return ProblemResults.Invalid(
                group is null ? "runs" : "group",
                group is null
                    ? "Runs of different agents cannot be shown as variants."
                    : $"The group has runs of several agents ({string.Join(", ", agentNames)}): add agent=<name>."
            );
        }

        var columns = new List<(RunResults, string?, IReadOnlyList<string>)>();
        var versions = new Dictionary<Guid, AgentVersionRow?>();
        foreach (var row in rows)
        {
            if (!versions.TryGetValue(row.AgentVersionId, out var version))
            {
                version = await agents.GetVersionAsync(row.AgentVersionId, cancellationToken);
                versions[row.AgentVersionId] = version;
            }
            columns.Add(
                (
                    RunReport.Results(
                        row,
                        await store.ListScenarioResultsAsync(row.Id, cancellationToken),
                        await store.ListCheckResultsAsync(row.Id, cancellationToken)
                    ),
                    version?.Label,
                    version is null ? [] : ContractContent.ModelNames(ContractContent.Read(version.Content))
                )
            );
        }
        return Results.Ok(
            RunVariantsReport.Build(agentNames[0], group, columns, await scenarios.ListLifetimesAsync(rows[0].AgentId, cancellationToken))
        );
    }
}
