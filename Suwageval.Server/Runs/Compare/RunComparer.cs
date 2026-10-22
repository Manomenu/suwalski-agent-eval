using Suwageval.Contracts.Runs;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Runs.Compare;

/// <summary>
/// The rules of comparing runs, as pure functions over their results: how their checks line up, the pass rate per tag, what counts as a
/// difference, the judge of each run and the changes against the baseline, the run asked for.
///
/// Words: a check is "passed" when its pass rate reaches its threshold; its pass rate is the share of its samples that passed. A run's
/// pass rate over some scenarios is the share of their checks that are passed — a check counts once, however many samples it has.
/// </summary>
internal static class RunComparer
{
    /// <summary>A turn time within this share of the baseline's counts as the same...</summary>
    public const double TimeSameShare = 0.05;

    /// <summary>...and so does one within this many milliseconds (a fast agent's noise).</summary>
    public const double TimeSameMs = 100;

    /// <summary>
    /// How <paramref name="runs"/> stand against the one at <paramref name="baselineColumn"/>, in the order of the runs;
    /// <paramref name="scenarios"/>: the agent's scenarios, for the tags.
    /// </summary>
    public static BaselineComparison Compare(IReadOnlyList<RunResults> runs, IReadOnlyList<ScenarioLifetime> scenarios, int baselineColumn)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(scenarios);
        var baseline = runs[baselineColumn];
        var baselineJudge = JudgeModel(baseline);
        return new BaselineComparison
        {
            RunId = baseline.Run.Id,
            Runs =
            [
                .. runs.Select(
                    (results, column) =>
                    {
                        var judge = JudgeModel(results);
                        return new ComparedRun
                        {
                            Run = results.Run,
                            JudgeModel = judge,
                            JudgeChanged = JudgeChanged(judge, baselineJudge),
                            PassRate = PassRate(results.Scenarios),
                            MedianMs = results.MedianMs,
                            P95Ms = results.P95Ms,
                            MedianChange = column == baselineColumn ? null : TimeChangeOf(results.MedianMs, baseline.MedianMs),
                            P95Change = column == baselineColumn ? null : TimeChangeOf(results.P95Ms, baseline.P95Ms),
                        };
                    }
                ),
            ],
            Tags = TagRows(runs, scenarios, baselineColumn),
            Checks = AlignRows(runs, baselineColumn),
        };
    }

    /// <summary>The checks of all runs lined up by scenario key and check key, in the order they first appear (the baseline's first).</summary>
    public static IReadOnlyList<ComparedCheck> AlignRows(IReadOnlyList<RunResults> runs, int baselineColumn)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var rows = new Dictionary<(string Scenario, string Check), CheckRunResult?[]>();
        var tagsOf = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var column in Enumerable.Range(0, runs.Count).OrderBy(column => column == baselineColumn ? 0 : 1))
        {
            foreach (var scenario in runs[column].Scenarios)
            {
                tagsOf[scenario.Key] = scenario.Tags;
                foreach (var check in scenario.Checks)
                {
                    if (!rows.TryGetValue((scenario.Key, check.Key), out var checks))
                    {
                        checks = new CheckRunResult?[runs.Count];
                        rows.Add((scenario.Key, check.Key), checks);
                    }
                    checks[column] = check;
                }
            }
        }
        return
        [
            .. rows.Select(row =>
            {
                var cells = row.Value.Select(check => check is null ? null : Cell(check)).ToList();
                // The check as the baseline ran it, else as the first run that has it.
                var shown = row.Value[baselineColumn] ?? row.Value.First(check => check is not null)!;
                var baseline = cells[baselineColumn];
                return new ComparedCheck
                {
                    ScenarioKey = row.Key.Scenario,
                    CheckKey = row.Key.Check,
                    ScenarioTags = tagsOf[row.Key.Scenario],
                    Definition = shown.Definition,
                    JudgedByModel = shown.JudgedByModel,
                    Differs = cells.Any(cell => !SameCell(cell, baseline)),
                    Comparable = cells.All(cell => cell is not null),
                    Cells = [.. cells.Select(cell => cell is null ? null : cell with { Trend = TrendOf(cell, baseline) })],
                };
            }),
        ];
    }

    private static ComparedCell Cell(CheckRunResult check) =>
        new()
        {
            Passed = check.Passed,
            PassRate = check.PassRate,
            SamplesPassed = check.Samples.Count(sample => sample.Passed),
            Samples = check.Samples.Count,
            NotJudged = check.Samples.Count(sample => sample.NotJudged),
            Revision = check.Revision,
            LatestRevision = check.LatestRevision,
            JudgedByModel = check.JudgedByModel,
            JudgeModel = check.JudgeModel,
        };

    private static bool SameCell(ComparedCell? a, ComparedCell? b) =>
        a is null || b is null ? a is null && b is null : a.Passed == b.Passed && a.PassRate == b.PassRate;

    /// <summary>How a cell stands against the baseline's: better, worse, the same, or null when either has no check to compare.</summary>
    public static Trend? TrendOf(ComparedCell? cell, ComparedCell? baseline)
    {
        if (cell is null || baseline is null)
        {
            return null;
        }
        if (cell.Passed != baseline.Passed)
        {
            return cell.Passed ? Trend.Better : Trend.Worse;
        }
        if (cell.PassRate == baseline.PassRate)
        {
            return Trend.Same;
        }
        return cell.PassRate > baseline.PassRate ? Trend.Better : Trend.Worse;
    }

    /// <summary>
    /// The model that judged a run: the most common known model among its model-judged checks (the first to reach the count on a tie; a
    /// run straddling a change of configuration is rare); null when no check was judged by a known model.
    /// </summary>
    public static string? JudgeModel(RunResults run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var counts = new Dictionary<string, int>();
        foreach (var check in run.Scenarios.SelectMany(scenario => scenario.Checks))
        {
            if (check.JudgedByModel && check.JudgeModel is { } model)
            {
                counts[model] = counts.GetValueOrDefault(model) + 1;
            }
        }
        string? best = null;
        foreach (var (model, count) in counts)
        {
            if (best is null || count > counts[best])
            {
                best = model;
            }
        }
        return best;
    }

    /// <summary>Another judge than the baseline's: both known and different — then the verdicts may differ because of the judge, not the agent.</summary>
    public static bool JudgeChanged(string? model, string? baselineModel) =>
        model is not null && baselineModel is not null && model != baselineModel;

    /// <summary>The share of the checks of these scenarios that are passed, 0 to 1; null when they have no checks.</summary>
    public static double? PassRate(IEnumerable<ScenarioRunResult> scenarios)
    {
        var checks = scenarios.SelectMany(scenario => scenario.Checks).ToList();
        return checks.Count == 0 ? null : checks.Count(check => check.Passed) / (double)checks.Count;
    }

    /// <summary>
    /// The pass rate over every check (no tag), then one row per tag, sorted by name. A run has a rate for a tag only when it ran every
    /// scenario of the tag (<see cref="TagCoverage"/>); a scenario the agent no longer has still carries its tags.
    /// </summary>
    public static IReadOnlyList<ComparedTag> TagRows(
        IReadOnlyList<RunResults> runs,
        IReadOnlyList<ScenarioLifetime> scenarios,
        int? baselineColumn = null
    )
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(scenarios);
        var tagsOf = scenarios.ToDictionary(scenario => scenario.Key, scenario => scenario.Tags);
        string[] TagsOf(string scenario)
        {
            return tagsOf.GetValueOrDefault(scenario) ?? [];
        }
        var completeTagsOf = runs.Select(run =>
                TagCoverage.CompleteTags(run.Run.CreatedAt, [.. run.Scenarios.Select(scenario => scenario.Key)], scenarios)
            )
            .ToList();
        var tags = runs.SelectMany(run => run.Scenarios)
            .SelectMany(scenario => TagsOf(scenario.Key))
            .Distinct()
            .Order(StringComparer.Ordinal);
        return
        [
            Row(null, [.. runs.Select(run => PassRate(run.Scenarios))], [], baselineColumn),
            .. tags.Select(tag =>
                Row(
                    tag,
                    [
                        .. runs.Select(
                            (run, column) =>
                                completeTagsOf[column].Contains(tag)
                                    ? PassRate(run.Scenarios.Where(scenario => TagsOf(scenario.Key).Contains(tag)))
                                    : null
                        ),
                    ],
                    [
                        .. runs.Select(run =>
                            TagCoverage.Counts(tag, run.Run.CreatedAt, [.. run.Scenarios.Select(scenario => scenario.Key)], scenarios)
                        ),
                    ],
                    baselineColumn
                )
            ),
        ];
    }

    private static ComparedTag Row(string? tag, List<double?> rates, List<(int Ran, int Total)> coverage, int? baselineColumn) =>
        new()
        {
            Tag = tag,
            Rates =
            [
                .. rates.Select(
                    (rate, column) =>
                        new ComparedRate(
                            rate,
                            baselineColumn is not { } baseline || column == baseline ? null : Change(rate, rates[baseline]),
                            coverage.Count > column ? coverage[column].Ran : null,
                            coverage.Count > column ? coverage[column].Total : null
                        )
                ),
            ],
        };

    /// <summary>A rate against the baseline's, in percentage points rounded to one decimal; null when either run has no such checks.</summary>
    public static RateChange? Change(double? rate, double? baseline)
    {
        if (rate is null || baseline is null)
        {
            return null;
        }
        var points = Math.Floor(((rate.Value - baseline.Value) * 1000) + 0.5) / 10;
        return new RateChange(
            points,
            points > 0 ? Trend.Better
                : points < 0 ? Trend.Worse
                : Trend.Same
        );
    }

    /// <summary>A turn time against the baseline's; for time lower is better, so better is faster. Null when either run made no call.</summary>
    public static TimeChange? TimeChangeOf(double? milliseconds, double? baseline)
    {
        if (milliseconds is null || baseline is null)
        {
            return null;
        }
        var change = milliseconds.Value - baseline.Value;
        var small = Math.Abs(change) < TimeSameMs || Math.Abs(change) < TimeSameShare * baseline.Value;
        return new TimeChange(
            change,
            small ? Trend.Same
                : change < 0 ? Trend.Better
                : Trend.Worse
        );
    }
}
