using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;
using Suwageval.Contracts.Runs;

namespace Suwageval.Server.Runs;

/// <summary>Turns the rows of a run into what the API sends: the summary and the results with pass rates.</summary>
internal static class RunReport
{
    /// <summary>Floating-point slack so that 4 of 5 samples reaching a threshold of 0.8 counts as reaching it.</summary>
    private const double Epsilon = 1e-9;

    public static RunSummary Summary(RunRow run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new RunSummary
        {
            Id = run.Id,
            Agent = run.AgentName,
            AgentVersionId = run.AgentVersionId,
            Tag = run.Tag,
            EndpointUrl = run.EndpointUrl,
            Status = run.State,
            Error = run.Error,
            SamplesTotal = run.SamplesTotal,
            SamplesDone = run.SamplesDone,
            ChecksPassed = run.ChecksPassed,
            ChecksFailed = run.ChecksFailed,
            ContractChangedDuringRun = run.ContractChangedDuringRun,
            CancelRequested = run.CancelRequested,
            CreatedAt = run.CreatedAt,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            RecheckOf = run.RecheckOf,
            SkippedScenarios = JsonSerializer.Deserialize<IReadOnlyList<SkippedScenario>>(run.SkippedScenarios, ContractJson.Options) ?? [],
        };
    }

    /// <summary>
    /// Per scenario its samples, and per check over those samples the pass rate (samples passed / samples with a verdict)
    /// against the threshold of the check revision that ran: the check passes when the rate reaches the threshold.
    /// </summary>
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
                return new ScenarioRunResult
                {
                    Key = group.Key,
                    Revision = group.Max(sample => sample.ScenarioRevision),
                    Samples = [.. group.OrderBy(sample => sample.Sample).Select(Sample)],
                    Checks = [.. checks],
                };
            });
        return new RunResults { Run = Summary(run), Scenarios = [.. scenarios] };
    }

    private static CheckRunResult CheckResult(string key, IReadOnlyList<CheckResultRow> rows, Dictionary<Guid, int> sampleNumberOf)
    {
        var passRate = rows.Count(row => row.Passed) / (double)rows.Count;
        var threshold = rows[0].PassThreshold;
        return new CheckRunResult
        {
            Key = key,
            Revision = rows.Max(row => row.CheckRevision),
            LatestRevision = rows[0].LatestRevision,
            Definition = JsonSerializer.Deserialize<CheckInput>(rows.MaxBy(row => row.CheckRevision)!.Definition, ContractJson.Options)!,
            Passed = passRate + Epsilon >= threshold,
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
            Undeclared = JsonSerializer.Deserialize<IReadOnlyList<string>>(row.Undeclared, ContractJson.Options) ?? [],
        };

    private static IReadOnlyList<ConversationItem>? Items(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<ConversationItem>>(json, ContractJson.Options);
}
