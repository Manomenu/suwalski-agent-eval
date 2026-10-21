using Suwageval.Contracts.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs;

/// <summary>The variants view of runs, as a pure function over their results: the checks and tags are counted by the rules of <see cref="RunComparer"/>.</summary>
internal static class RunVariantsReport
{
    /// <summary>The most runs a variants view holds.</summary>
    public const int MaxRuns = 20;

    public static RunVariants Build(
        string agent,
        string? group,
        IReadOnlyList<(RunResults Results, string? VersionLabel, IReadOnlyList<string> ModelNames)> runs,
        IReadOnlyList<ScenarioLifetime> scenarios
    )
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(scenarios);
        var tagsOf = scenarios.ToDictionary(scenario => scenario.Key, scenario => (IReadOnlyList<string>)scenario.Tags);
        var keys = runs.SelectMany(run => run.Results.Scenarios).Select(scenario => scenario.Key).Distinct().Order(StringComparer.Ordinal);
        return new RunVariants
        {
            Agent = agent,
            Group = group,
            Runs =
            [
                .. runs.Select(run => new VariantRun
                {
                    Run = run.Results.Run,
                    VersionLabel = run.VersionLabel,
                    ModelNames = run.ModelNames,
                    MedianMs = run.Results.MedianMs,
                    P95Ms = run.Results.P95Ms,
                }),
            ],
            Tags = RunComparer.TagRows([.. runs.Select(run => run.Results)], scenarios),
            Scenarios =
            [
                .. keys.Select(key => new VariantScenario
                {
                    Key = key,
                    Tags = tagsOf.GetValueOrDefault(key) ?? [],
                    Cells =
                    [
                        .. runs.Select(run =>
                            run.Results.Scenarios.FirstOrDefault(scenario => scenario.Key == key) is { } scenario ? Cell(scenario) : null
                        ),
                    ],
                }),
            ],
        };
    }

    private static VariantCell Cell(ScenarioRunResult scenario) =>
        new()
        {
            ChecksPassed = scenario.Checks.Count(check => check.Passed),
            ChecksTotal = scenario.Checks.Count,
            FailedChecks = [.. scenario.Checks.Where(check => !check.Passed).Select(check => check.Key)],
            Samples = scenario.Samples.Count,
            SamplesFailed = scenario.Samples.Count(sample => sample.Status == SampleStatus.Failed),
        };
}
