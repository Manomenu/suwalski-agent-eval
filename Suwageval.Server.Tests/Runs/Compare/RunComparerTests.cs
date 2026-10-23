using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;
using Suwageval.Server.Runs.Compare;
using Suwageval.Server.Scenarios;

namespace Suwageval.Server.Tests.Runs.Compare;

/// <summary>The rules of comparing runs on results built by hand (the web's own tests of them, ported).</summary>
public sealed class RunComparerTests
{
    /// <summary>A check with <paramref name="passes"/> of <paramref name="samples"/> samples passed, against a threshold of 1 unless given.</summary>
    private static CheckRunResult Check(
        string key,
        int passes,
        int samples,
        double threshold = 1,
        bool judgedByModel = false,
        string? judgeModel = null,
        int notJudged = 0
    ) =>
        new()
        {
            Key = key,
            Revision = 1,
            LatestRevision = 1,
            Definition = SeededAgent.RefundAfterLookup(key, "s"),
            JudgedByModel = judgedByModel,
            JudgeModel = judgeModel,
            Passed = RunReport.Reaches(passes, samples, threshold),
            PassedFully = RunReport.Reaches(passes, samples, threshold) && notJudged == 0,
            PassRate = passes / (double)samples,
            PassThreshold = threshold,
            Samples =
            [
                .. Enumerable
                    .Range(0, samples)
                    .Select(index => new CheckSampleResult
                    {
                        Sample = index + 1,
                        Passed = index < passes,
                        NotJudged = index >= samples - notJudged,
                        Evidence = [],
                        Message = "",
                    }),
            ],
        };

    private static ScenarioRunResult Scenario(string key, params CheckRunResult[] checks) =>
        new()
        {
            Key = key,
            Tags = [],
            Revision = 1,
            DefinitionYaml = "",
            Samples = [],
            Checks = checks,
            PassedFully = false,
        };

    private static RunResults Run(IReadOnlyList<ScenarioRunResult> scenarios, double? medianMs = null, double? p95Ms = null) =>
        new()
        {
            Run = new RunSummary
            {
                Id = Guid.NewGuid(),
                Agent = "agent",
                AgentVersionId = Guid.NewGuid(),
                EndpointUrl = "http://agent.test",
                Status = RunStatus.Done,
                SamplesTotal = 0,
                SamplesDone = 0,
                ChecksPassed = 0,
                ChecksFailed = 0,
                CreatedAt = DateTime.UtcNow,
            },
            Scenarios = scenarios,
            MedianMs = medianMs,
            P95Ms = p95Ms,
        };

    private static RunResults Run(params ScenarioRunResult[] scenarios) => Run(scenarios, null, null);

    private static ScenarioLifetime Life(string key, params string[] tags) =>
        new()
        {
            Key = key,
            Tags = tags,
            CreatedAt = DateTime.MinValue,
        };

    private static IEnumerable<string> Keys(IEnumerable<ComparedCheck> rows) => rows.Select(row => $"{row.ScenarioKey}/{row.CheckKey}");

    [Fact]
    public void Checks_line_up_by_scenario_key_and_check_key_in_order_of_appearance()
    {
        var baseline = Run(Scenario("s1", Check("a", 1, 1), Check("b", 0, 1)));
        var other = Run(Scenario("s1", Check("b", 1, 1), Check("c", 1, 1)), Scenario("s2", Check("a", 1, 1)));

        var rows = RunComparer.AlignRows([baseline, other], 0);

        Assert.Equal(["s1/a", "s1/b", "s1/c", "s2/a"], Keys(rows));
        Assert.Equal([true, null], rows[0].Cells.Select(cell => cell?.Passed));
        Assert.Equal([false, true], rows[1].Cells.Select(cell => cell?.Passed));
        Assert.Equal([null, true], rows[3].Cells.Select(cell => cell?.Passed));
        Assert.Equal(2, rows.Count(row => row.CheckKey == "a"));
    }

    [Fact]
    public void A_cell_takes_the_verdict_the_pass_rate_and_the_samples_that_passed()
    {
        var below = RunComparer.AlignRows([Run(Scenario("s", Check("c", 3, 5))), Run(Scenario("s", Check("c", 3, 5, 0.6)))], 0).Single();

        Assert.Equal(
            (false, 0.6, 3, 5, 0),
            (
                below.Cells[0]!.Passed,
                below.Cells[0]!.PassRate,
                below.Cells[0]!.SamplesPassed,
                below.Cells[0]!.Samples,
                below.Cells[0]!.NotJudged
            )
        );
        Assert.True(below.Cells[1]!.Passed);
    }

    [Fact]
    public void A_cell_counts_the_samples_nobody_could_judge()
    {
        var row = RunComparer.AlignRows([Run(Scenario("s", Check("c", 0, 2, notJudged: 1)))], 0).Single();

        Assert.Equal(1, row.Cells[0]!.NotJudged);
    }

    [Fact]
    public void A_row_differs_when_a_verdict_or_a_pass_rate_differs_or_a_check_is_missing_from_some_runs_only()
    {
        static ComparedCheck Row(params CheckRunResult?[] checks)
        {
            return RunComparer
                .AlignRows([.. checks.Select(check => Run(check is null ? Scenario("s") : Scenario("s", check)))], 0)
                .Single();
        }

        var pass = Check("c", 1, 1);
        var fail = Check("c", 0, 1);
        var half = Check("c", 1, 2, 0.5);

        Assert.False(Row(pass, pass).Differs);
        Assert.True(Row(pass, fail).Differs);
        Assert.True(Row(pass, half).Differs);
        Assert.False(Row(half, half, half).Differs);
        Assert.True(Row(pass, null).Differs);
        Assert.True(Row(null, pass).Differs);
        Assert.True(Row(pass, fail).Comparable);
        Assert.False(Row(pass, null).Comparable);
    }

    [Fact]
    public void A_cell_is_better_or_worse_by_the_verdict_first_then_by_the_pass_rate()
    {
        var rows = RunComparer.AlignRows(
            [
                Run(Scenario("s", Check("a", 0, 3), Check("b", 1, 3), Check("c", 3, 3), Check("d", 1, 3))),
                Run(Scenario("s", Check("a", 3, 3), Check("b", 2, 3), Check("c", 3, 3), Check("e", 1, 1))),
                Run(Scenario("s", Check("a", 0, 3), Check("b", 0, 3), Check("c", 0, 3), Check("d", 1, 3))),
            ],
            0
        );

        Assert.Equal(
            [Contracts.Runs.Trend.Same, Contracts.Runs.Trend.Better, Contracts.Runs.Trend.Same],
            rows.Single(row => row.CheckKey == "a").Cells.Select(cell => cell?.Trend)
        );
        Assert.Equal(
            [Contracts.Runs.Trend.Same, Contracts.Runs.Trend.Better, Contracts.Runs.Trend.Worse],
            rows.Single(row => row.CheckKey == "b").Cells.Select(cell => cell?.Trend)
        );
        Assert.Equal(
            [Contracts.Runs.Trend.Same, Contracts.Runs.Trend.Same, Contracts.Runs.Trend.Worse],
            rows.Single(row => row.CheckKey == "c").Cells.Select(cell => cell?.Trend)
        );
        // A check one of the runs lacks has nothing to compare.
        Assert.Equal(
            [Contracts.Runs.Trend.Same, null, Contracts.Runs.Trend.Same],
            rows.Single(row => row.CheckKey == "d").Cells.Select(cell => cell?.Trend)
        );
        Assert.Equal([null, null, null], rows.Single(row => row.CheckKey == "e").Cells.Select(cell => cell?.Trend));
    }

    [Fact]
    public void A_check_is_shown_as_the_baseline_ran_it_else_as_the_first_run_that_has_it()
    {
        var first = Check("c", 1, 1) with { Revision = 1, JudgedByModel = false };
        var second = Check("c", 1, 1) with { Revision = 2, JudgedByModel = true };

        var both = RunComparer.AlignRows([Run(Scenario("s", first)), Run(Scenario("s", second))], 0).Single();
        var late = RunComparer.AlignRows([Run(Scenario("s")), Run(Scenario("s", second))], 0).Single();

        Assert.False(both.JudgedByModel);
        Assert.True(late.JudgedByModel);
        Assert.Equal([null, 2], late.Cells.Select(cell => cell?.Revision));
    }

    [Fact]
    public void A_pass_rate_is_the_share_of_passed_checks_a_check_counting_once()
    {
        var refund = Scenario("refund", Check("called", 1, 1), Check("polite", 0, 1));
        var greet = Scenario("greet", Check("hello", 1, 1));

        Assert.Equal(0.5, RunComparer.PassRate([refund]));
        Assert.Equal(2 / 3.0, RunComparer.PassRate([refund, greet]));
        Assert.Null(RunComparer.PassRate([]));
    }

    [Fact]
    public void An_aligned_check_carries_the_tags_of_its_scenario()
    {
        var tagged = Scenario("refund", Check("called", 1, 1)) with { Tags = ["billing", "smoke"] };

        var row = RunComparer.AlignRows([Run(tagged)], 0).Single();

        Assert.Equal(["billing", "smoke"], row.ScenarioTags);
    }

    [Fact]
    public void Tag_rows_have_all_first_then_the_tags_sorted_and_a_scenario_without_tags_is_only_in_all()
    {
        var refund = Scenario("refund", Check("called", 1, 1), Check("polite", 0, 1));
        var greet = Scenario("greet", Check("hello", 1, 1));
        var gone = Scenario("gone", Check("old", 0, 1));
        var tags = new[] { Life("refund", "billing", "critical"), Life("greet", "smoke") };

        var rows = RunComparer.TagRows([Run(refund, greet, gone)], tags);

        Assert.Equal([null, "billing", "critical", "smoke"], rows.Select(row => row.Tag));
        Assert.Equal(0.5, rows[0].Rates[0].PassRate);
        Assert.Equal(0.5, rows[1].Rates[0].PassRate);
        Assert.Equal(1, rows[3].Rates[0].PassRate);
    }

    [Fact]
    public void A_run_without_a_check_of_a_tag_has_no_rate_and_the_others_change_is_against_the_first()
    {
        var refund = Scenario("refund", Check("called", 1, 1), Check("polite", 0, 1));
        var greet = Scenario("greet", Check("hello", 1, 1));
        var tags = new[] { Life("refund", "billing"), Life("greet", "smoke") };

        var rows = RunComparer.TagRows([Run(refund), Run(greet), Run(refund, greet)], tags, 0);

        var billing = rows.Single(row => row.Tag == "billing");
        Assert.Equal([0.5, null, 0.5], billing.Rates.Select(rate => rate.PassRate));
        Assert.Equal([null, null, new RateChange(0, Contracts.Runs.Trend.Same)], billing.Rates.Select(rate => rate.Change));
        var all = rows[0];
        Assert.Equal(new RateChange(50, Contracts.Runs.Trend.Better), all.Rates[1].Change);
        Assert.Null(all.Rates[0].Change);
    }

    [Fact]
    public void A_run_that_ran_only_part_of_a_tags_scenarios_has_no_rate_for_the_tag_while_all_keeps_its_checks()
    {
        var refund = Scenario("refund", Check("called", 1, 1));
        var chargeback = Scenario("chargeback", Check("called", 0, 1));
        var tags = new[] { Life("refund", "billing"), Life("chargeback", "billing") };

        var rows = RunComparer.TagRows([Run(refund, chargeback), Run(refund)], tags);

        Assert.Equal([0.5, 1], rows[0].Rates.Select(rate => rate.PassRate));
        Assert.Equal([0.5, null], rows.Single(row => row.Tag == "billing").Rates.Select(rate => rate.PassRate));
    }

    [Fact]
    public void A_rate_change_is_in_percentage_points_rounded_to_one_decimal()
    {
        Assert.Equal(new RateChange(25, Contracts.Runs.Trend.Better), RunComparer.Change(0.75, 0.5));
        Assert.Equal(new RateChange(-16.7, Contracts.Runs.Trend.Worse), RunComparer.Change(0.5, 2 / 3.0));
        Assert.Equal(new RateChange(0, Contracts.Runs.Trend.Same), RunComparer.Change(0.5, 0.5));
        Assert.Equal(Contracts.Runs.Trend.Same, RunComparer.Change(0.5001, 0.5)!.Trend);
        Assert.Null(RunComparer.Change(null, 0.5));
        Assert.Null(RunComparer.Change(0.5, null));
    }

    [Fact]
    public void A_turn_time_is_better_when_faster_and_worse_when_slower()
    {
        Assert.Equal(new TimeChange(-2000, Contracts.Runs.Trend.Better), RunComparer.TimeChangeOf(8000, 10_000));
        Assert.Equal(new TimeChange(2000, Contracts.Runs.Trend.Worse), RunComparer.TimeChangeOf(12_000, 10_000));
    }

    [Fact]
    public void A_turn_time_is_the_same_under_five_percent_of_the_baseline_or_under_the_noise_floor()
    {
        Assert.Equal(Contracts.Runs.Trend.Same, RunComparer.TimeChangeOf(10_400, 10_000)!.Trend);
        Assert.Equal(Contracts.Runs.Trend.Worse, RunComparer.TimeChangeOf(10_500, 10_000)!.Trend);
        Assert.Equal(Contracts.Runs.Trend.Same, RunComparer.TimeChangeOf(400 + RunComparer.TimeSameMs - 1, 400)!.Trend);
        Assert.Equal(Contracts.Runs.Trend.Worse, RunComparer.TimeChangeOf(400 + RunComparer.TimeSameMs, 400)!.Trend);
    }

    [Fact]
    public void A_turn_time_has_no_change_when_either_run_made_no_call()
    {
        Assert.Null(RunComparer.TimeChangeOf(null, 1000));
        Assert.Null(RunComparer.TimeChangeOf(1000, null));
    }

    [Fact]
    public void A_run_is_judged_by_the_most_common_known_model_among_its_model_judged_checks()
    {
        static CheckRunResult Judged(string key, string? model)
        {
            return Check(key, 1, 1, judgedByModel: true, judgeModel: model);
        }

        Assert.Equal("test-judge-model", RunComparer.JudgeModel(Run(Scenario("s", Judged("c", "test-judge-model"), Check("d", 1, 1)))));
        Assert.Equal(
            "test-judge-model",
            RunComparer.JudgeModel(
                Run(Scenario("s", Judged("a", "other-test-model"), Judged("b", "test-judge-model"), Judged("c", "test-judge-model")))
            )
        );
        Assert.Null(RunComparer.JudgeModel(Run(Scenario("s", Check("c", 1, 1)))));
        Assert.Null(RunComparer.JudgeModel(Run(Scenario("s", Judged("c", null)))));
        // A model named on a deterministic check does not count.
        Assert.Null(RunComparer.JudgeModel(Run(Scenario("s", Check("c", 1, 1, judgeModel: "stray")))));
    }

    [Fact]
    public void The_judge_changed_only_between_two_known_different_models()
    {
        Assert.True(RunComparer.JudgeChanged("test-judge-model", "other-test-model"));
        Assert.False(RunComparer.JudgeChanged("test-judge-model", "test-judge-model"));
        Assert.False(RunComparer.JudgeChanged(null, "test-judge-model"));
        Assert.False(RunComparer.JudgeChanged("test-judge-model", null));
    }

    [Fact]
    public void A_comparison_carries_each_runs_judge_pass_rate_and_turn_time_changes_against_the_first()
    {
        static ScenarioRunResult Judged(string model)
        {
            return Scenario("s", Check("c", 1, 1, judgedByModel: true, judgeModel: model));
        }

        var comparison = RunComparer.Compare(
            [
                Run([Judged("a")], medianMs: 1000, p95Ms: 2000),
                Run([Judged("b")], medianMs: 1500, p95Ms: null),
                Run([Judged("a")], medianMs: 1010, p95Ms: 2000),
            ],
            [],
            0
        );

        Assert.Equal(["a", "b", "a"], comparison.Runs.Select(run => run.JudgeModel));
        Assert.Equal([false, true, false], comparison.Runs.Select(run => run.JudgeChanged));
        Assert.Equal([1.0, 1.0, 1.0], comparison.Runs.Select(run => run.PassRate));
        Assert.Equal(
            [null, new TimeChange(500, Contracts.Runs.Trend.Worse), new TimeChange(10, Contracts.Runs.Trend.Same)],
            comparison.Runs.Select(run => run.MedianChange)
        );
        Assert.Equal([null, null, new TimeChange(0, Contracts.Runs.Trend.Same)], comparison.Runs.Select(run => run.P95Change));
        Assert.Equal([null], comparison.Tags.Select(row => row.Tag));
        Assert.Equal(comparison.Runs[0].Run.Id, comparison.RunId);
    }

    [Fact]
    public void The_baseline_is_the_run_asked_for_not_the_first_and_every_column_keeps_its_place()
    {
        var comparison = RunComparer.Compare(
            [
                Run([Scenario("s", Check("c", 1, 1))], medianMs: 1000),
                Run([Scenario("s", Check("c", 0, 1))], medianMs: 2000),
                Run([Scenario("s", Check("c", 1, 1))], medianMs: 1000),
            ],
            [],
            1
        );

        Assert.Equal(comparison.Runs[1].Run.Id, comparison.RunId);
        Assert.Equal(
            [new TimeChange(-1000, Contracts.Runs.Trend.Better), null, new TimeChange(-1000, Contracts.Runs.Trend.Better)],
            comparison.Runs.Select(run => run.MedianChange)
        );
        Assert.Equal(
            [Contracts.Runs.Trend.Better, Contracts.Runs.Trend.Same, Contracts.Runs.Trend.Better],
            comparison.Checks.Single().Cells.Select(cell => cell!.Trend)
        );
        Assert.Equal(
            [new RateChange(100, Contracts.Runs.Trend.Better), null, new RateChange(100, Contracts.Runs.Trend.Better)],
            comparison.Tags[0].Rates.Select(rate => rate.Change)
        );
    }

    [Fact]
    public void Every_column_names_its_judge_and_every_check_says_whether_a_model_judges_it_without_a_baseline()
    {
        var judged = Run(Scenario("s", Check("c", 1, 1, judgedByModel: true, judgeModel: "test-judge-model"), Check("d", 1, 1)));
        var plain = Run(Scenario("s", Check("d", 1, 1)));

        var comparison = RunComparisonReport.Build("agent", null, [(judged, null, []), (plain, null, [])], [], baselineColumn: null);

        Assert.Null(comparison.Baseline);
        Assert.Equal(["test-judge-model", null], comparison.Runs.Select(run => run.JudgeModel));
        var checks = comparison.Scenarios.Single().Cells[0]!.Checks;
        Assert.Equal([true, false], checks.Select(check => check.JudgedByModel));
    }
}
