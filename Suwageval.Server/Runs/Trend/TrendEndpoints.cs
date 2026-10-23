using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;
using Suwageval.Server.Problems;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs.Trend;

/// <summary>The route of the pass-rate trend of an agent.</summary>
internal static class TrendEndpoints
{
    public static IEndpointRouteBuilder MapTrend(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/agents/{agent}/pass-rates", ListAsync).Produces<IReadOnlyList<RunPassRates>>();
        return app;
    }

    /// <summary>
    /// The pass-rate trend of an agent: its finished runs that called the agent (the newest <see cref="PassRateTrend.MaxRuns"/>, oldest first) with
    /// the rate over all checks and per tag and the turn time (median, p95) over the same samples. A run has a rate for a tag only when it ran every scenario of the tag (<see cref="TagCoverage"/>); a run with no check is left out. 404: no such agent.
    /// </summary>
    private static async Task<IResult> ListAsync(
        string agent,
        AgentStore agents,
        ScenarioStore scenarios,
        RunStore runs,
        TrendStore trend,
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
        var checksOf = (await trend.ListTrendChecksAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
        var durationsOf = (await trend.ListTrendDurationsAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
        var ranOf = (await trend.ListTrendScenariosAsync(shownIds, cancellationToken)).ToLookup(row => row.RunId);
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
}
