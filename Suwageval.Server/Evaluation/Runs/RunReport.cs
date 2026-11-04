using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;
using Suwageval.Server.Evaluation.Scenarios;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

internal static class RunReport
{
    // Why: floating-point slack so that 4 of 5 samples reaching a threshold of 0.8 counts as reaching it
    private const double Epsilon = 1e-9;

    public static bool Reaches(int passed, int total, double threshold) => (passed / (double)total) + Epsilon >= threshold;

    public static RunSummary Summary(RunRow run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new RunSummary
        {
            Id = run.Id,
            Agent = run.AgentName,
            AgentVersionId = run.AgentVersionId,
            Tags = run.Tags,
            EndpointUrl = run.EndpointUrl,
            Group = run.RunGroup,
            Label = run.Label,
            DataVersion = run.DataVersion,
            DataDescription = run.DataDescription,
            Note = run.Note,
            Status = run.State,
            Error = run.Error,
            SamplesTotal = run.SamplesTotal,
            SamplesDone = run.SamplesDone,
            SamplesFailed = run.SamplesFailed,
            ChecksPassed = run.ChecksPassed,
            ChecksFailed = run.ChecksFailed,
            ContractChangedDuringRun = run.ContractChangedDuringRun,
            CancelRequested = run.CancelRequested,
            CreatedAt = run.CreatedAt,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            RecheckOf = run.RecheckOf,
            Origin = run.Placement.Origin,
            Reference = run.Reference,
            ComparedWith = run.ComparedWith,
            JudgeModels = JsonSerializer.Deserialize<IReadOnlyList<string>>(run.JudgeModels, ContractJson.Options) ?? [],
            SkippedScenarios = JsonSerializer.Deserialize<IReadOnlyList<SkippedScenario>>(run.SkippedScenarios, ContractJson.Options) ?? [],
        };
    }

    public static RunResults Results(RunRow run, IReadOnlyList<ScenarioResultRow> samples, IReadOnlyList<CheckResultRow> verdicts)
    {
        var sampleNumberOf = samples.ToDictionary(sample => sample.Id, sample => sample.Sample);
        var verdictsOf = verdicts.ToLookup(verdict => verdict.ScenarioResultId);
        var scenarios = samples
            .GroupBy(sample => sample.ScenarioKey)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var checks = group
                    .SelectMany(sample => verdictsOf[sample.Id])
                    .GroupBy(verdict => verdict.CheckKey)
                    .OrderBy(byCheck => byCheck.Key, StringComparer.Ordinal)
                    .Select(byCheck => CheckResult(byCheck.Key, [.. byCheck], sampleNumberOf));
                // Why: a group of samples is never empty
                var latest = group.MaxBy(sample => sample.ScenarioRevision)!;
                return new ScenarioRunResult
                {
                    Key = group.Key,
                    AcGuardrail = Guardrail(run, Definition(latest).AcGuardrail, [.. group], verdictsOf),
                    // Why: the tags column is always a JSON array written by the server
                    Tags = JsonSerializer.Deserialize<string[]>(group.First().ScenarioTagsJson)!,
                    Revision = group.Max(sample => sample.ScenarioRevision),
                    DefinitionYaml = DefinitionYaml(latest),
                    Samples = [.. group.OrderBy(sample => sample.Sample).Select(Sample)],
                    Checks = [.. checks],
                    PassedFully = false,
                };
            });
        var summary = Summary(run);
        var scenarioResults = scenarios.Select(WithPassedFully).ToList();
        var results = new RunResults
        {
            Run = summary,
            Scenarios = scenarioResults,
            Gate = run.Gate && run.State == RunStatus.Done ? GateOf(scenarioResults, summary.SkippedScenarios) : null,
        };
        var durations = TurnTimes.Durations(results.Scenarios);
        return results with { MedianMs = TurnTimes.Median(durations), P95Ms = TurnTimes.P95(durations) };
    }

    private static ScenarioRunResult WithPassedFully(ScenarioRunResult scenario) =>
        scenario with
        {
            PassedFully =
                scenario.Checks.Count > 0
                && scenario.Checks.All(check => check.PassedFully)
                && scenario.Samples.All(sample => sample.Status == SampleStatus.Done),
        };

    private static ScenarioInput Definition(ScenarioResultRow sample) =>
        // Why: stored scenario content was serialized by the server, so it is valid JSON
        JsonSerializer.Deserialize<ScenarioInput>(sample.ScenarioContent, ContractJson.Options)!;

    private static string DefinitionYaml(ScenarioResultRow sample) =>
        // Why: the tags column is always a JSON array written by the server
        ScenarioYaml.Of(Definition(sample) with { Tags = JsonSerializer.Deserialize<string[]>(sample.ScenarioTagsJson)! });

    private static ScenarioGuardrailResult? Guardrail(
        RunRow run,
        AcGuardrail? guardrail,
        IReadOnlyList<ScenarioResultRow> attempts,
        ILookup<Guid, CheckResultRow> verdictsOf
    )
    {
        if (guardrail is null)
        {
            return null;
        }
        if (run.GateRetryLimit is not { } agentRetryLimit)
        {
            return new ScenarioGuardrailResult { Id = guardrail.Id };
        }
        var limit = AcGuardrailRules.EffectiveRetryLimit(guardrail.RetryLimit, agentRetryLimit);
        var passed = attempts.Any(attempt =>
            attempt.Status == nameof(SampleStatus.Done)
            && verdictsOf[attempt.Id].Any()
            && verdictsOf[attempt.Id].All(verdict => verdict.Passed)
        );
        return new ScenarioGuardrailResult
        {
            Id = guardrail.Id,
            RetryLimit = limit,
            Attempts = attempts.Count,
            Passed =
                passed ? true
                : attempts.Count >= limit ? false
                : null,
        };
    }

    private static GateResult GateOf(IReadOnlyList<ScenarioRunResult> scenarios, IReadOnlyList<SkippedScenario> skipped)
    {
        var failed = scenarios.FirstOrDefault(scenario => scenario.AcGuardrail?.Passed == false)?.AcGuardrail?.Id;
        return new GateResult
        {
            Passed = failed is null && skipped.Count == 0 && scenarios.All(scenario => scenario.AcGuardrail?.Passed == true),
            FailedGuardrail = failed,
        };
    }

    private static CheckRunResult CheckResult(string key, IReadOnlyList<CheckResultRow> rows, Dictionary<Guid, int> sampleNumberOf)
    {
        var passed = rows.Count(row => row.Passed);
        var passRate = passed / (double)rows.Count;
        var threshold = rows[0].PassThreshold;
        // Why: a group of check rows is never empty and its definition was serialized by the server
        var definition = JsonSerializer.Deserialize<CheckInput>(rows.MaxBy(row => row.CheckRevision)!.Definition, ContractJson.Options)!;
        return new CheckRunResult
        {
            Key = key,
            Revision = rows.Max(row => row.CheckRevision),
            LatestRevision = rows[0].LatestRevision,
            Definition = definition,
            JudgedByModel = CheckEvaluator.JudgedByModel(definition),
            JudgeModel = rows.Where(row => row.JudgeModel is not null)
                .GroupBy(row => row.JudgeModel)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault(),
            Passed = Reaches(passed, rows.Count, threshold),
            // Why: a verdict is open while a sample is not judged, so only a fully judged sample set passes for good
            PassedFully = Reaches(passed, rows.Count, threshold) && !rows.Any(row => row.NotJudged),
            PassRate = passRate,
            PassThreshold = threshold,
            Samples =
            [
                .. rows.OrderBy(row => sampleNumberOf[row.ScenarioResultId])
                    .Select(row => new CheckSampleResult
                    {
                        Sample = sampleNumberOf[row.ScenarioResultId],
                        Passed = row.Passed,
                        NotJudged = row.NotJudged,
                        Evidence = Items(row.Evidence) ?? [],
                        Message = row.Message,
                    }),
            ],
        };
    }

    private static SampleResult Sample(ScenarioResultRow row) =>
        new()
        {
            Sample = row.Sample,
            Status = Enum.Parse<SampleStatus>(row.Status),
            Output = row.Output is null ? null : Items(row.Output),
            Error = row.Error,
            DurationMs = row.DurationMs,
            TurnDurationsMs = row.TurnDurationsMs,
            EndedByUser = row.EndedByUser,
            Undeclared = JsonSerializer.Deserialize<IReadOnlyList<string>>(row.Undeclared, ContractJson.Options) ?? [],
        };

    private static IReadOnlyList<ConversationItem>? Items(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<ConversationItem>>(json, ContractJson.Options);
}
