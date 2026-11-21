using Microsoft.AspNetCore.Mvc;
using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Problems;

namespace Suwageval.Server.Evaluation.Runs.Compare;

internal static class CompareEndpoints
{
    public static IEndpointRouteBuilder MapCompare(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/runs/compare", CompareAsync)
            .Produces<RunComparison>()
            .ProducesRefusal(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);
        return app;
    }

    private static async Task<IResult> CompareAsync(
        [FromQuery] Guid[]? runs,
        [FromQuery] string? group,
        [FromQuery] string? agent,
        [FromQuery] Guid? baseline,
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
                ids.Count > 0 ? "Both the runs and a group are given." : "Neither the runs nor a group is given.",
                "Give the runs to show (runs) or the group they are in (group), not both and not neither."
            );
        }
        var rows = new List<RunRow>();
        if (ids.Count > 0)
        {
            if (ids.Count > RunComparisonReport.MaxRuns)
            {
                return ProblemResults.Invalid(
                    "runs",
                    $"More than {RunComparisonReport.MaxRuns} runs are chosen.",
                    $"Choose at most {RunComparisonReport.MaxRuns} runs."
                );
            }
            foreach (var id in ids)
            {
                var row = await store.GetAsync(id, cancellationToken);
                if (row is null)
                {
                    return ProblemResults.RunNotFound(id);
                }
                rows.Add(row);
            }
            group = null;
        }
        else
        {
            if (RunGroupRules.Problem(group) is { } problem)
            {
                return ProblemResults.Invalid("group", problem, RunGroupRules.Fix);
            }
            Guid? agentId = null;
            if (!string.IsNullOrEmpty(agent))
            {
                var found = await agents.GetAsync(agent, cancellationToken);
                if (found is null)
                {
                    return ProblemResults.AgentNotFound(agent);
                }
                agentId = found.Id;
            }
            var newestFirst = await store.ListPageAsync(
                agentId,
                [],
                0,
                RunComparisonReport.MaxRuns + 1,
                group,
                cancellationToken: cancellationToken
            );
            if (newestFirst.Count == 0)
            {
                return ProblemResults.RunGroupNotFound(
                    group
                        ?? throw new InvalidOperationException(
                            "Request validation allows exactly one of runs and group, and this branch has no runs."
                        )
                );
            }
            if (newestFirst.Count > RunComparisonReport.MaxRuns)
            {
                return ProblemResults.Invalid(
                    "group",
                    $"The group has more than {RunComparisonReport.MaxRuns} runs.",
                    $"Name at most {RunComparisonReport.MaxRuns} of its runs to show (runs)."
                );
            }
            rows = [.. newestFirst.Reverse()];
        }
        var agentNames = rows.Select(row => row.AgentName).Distinct().ToList();
        if (agentNames.Count > 1)
        {
            return ProblemResults.Invalid(
                group is null ? "runs" : "group",
                group is null
                    ? "Runs of different agents cannot be compared."
                    : $"The group has runs of several agents ({string.Join(", ", agentNames)}).",
                group is null ? "Choose runs of one agent." : "Add agent=<name> to compare the runs of one of them."
            );
        }

        int? baselineColumn = null;
        if (baseline is { } baselineId)
        {
            var index = rows.FindIndex(row => row.Id == baselineId);
            if (index < 0)
            {
                return ProblemResults.Invalid(
                    "baseline",
                    "The baseline is not one of the compared runs.",
                    "Choose one of the compared runs as the baseline."
                );
            }
            if (rows.Count < 2)
            {
                return ProblemResults.Invalid(
                    "baseline",
                    "A baseline needs at least two runs to compare.",
                    "Compare two runs or more, or leave the baseline out."
                );
            }
            baselineColumn = index;
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
        var report = RunComparisonReport.Build(
            agentNames[0],
            group,
            columns,
            await scenarios.ListLifetimesAsync(rows[0].AgentId, cancellationToken),
            baselineColumn
        );
        return Results.Ok(
            group is null ? report : report with { GroupNote = await store.GetGroupNoteAsync(rows[0].AgentId, group, cancellationToken) }
        );
    }
}
