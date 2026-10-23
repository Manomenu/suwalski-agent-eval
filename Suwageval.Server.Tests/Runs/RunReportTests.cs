using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

/// <summary>What a run's results say about a check or a scenario that "passed fully": the one place the run page's "Hide passed
/// checks" and <c>results --failed-only</c> take it from.</summary>
public sealed class RunReportTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static RunRow Run() =>
        new(
            RunId,
            Guid.NewGuid(),
            "agent",
            Guid.NewGuid(),
            "[]",
            "http://agent.test",
            "Done",
            null,
            0,
            0,
            0,
            0,
            false,
            false,
            DateTime.UtcNow,
            null,
            null
        );

    private static ScenarioResultRow Sample(string scenario, int sample, string status = "Done") =>
        new(
            Guid.NewGuid(),
            scenario,
            1,
            sample,
            status,
            null,
            status == "Done" ? null : "timeout",
            "[]",
            null,
            Guid.NewGuid(),
            JsonSerializer.Serialize(SeededAgent.Scenario(scenario, []), ContractJson.Options),
            "[]",
            null,
            false
        );

    private static CheckResultRow Verdict(ScenarioResultRow sample, string check, bool passed, bool notJudged = false) =>
        new(
            sample.Id,
            check,
            1,
            1,
            JsonSerializer.Serialize<CheckInput>(SeededAgent.RefundAfterLookup(check, sample.ScenarioKey), ContractJson.Options),
            1,
            passed,
            notJudged,
            "[]",
            "",
            Guid.NewGuid(),
            null
        );

    [Fact]
    public void A_scenario_passed_fully_only_with_every_check_passed_and_judged_and_every_sample_answered()
    {
        var clean = Sample("clean", 1);
        var unjudged = Sample("unjudged", 1);
        var noAnswer = Sample("no-answer", 1);
        var noAnswerFailed = Sample("no-answer", 2, "Failed");
        var noChecks = Sample("unchecked", 1);

        var results = RunReport.Results(
            Run(),
            [clean, unjudged, noAnswer, noAnswerFailed, noChecks],
            [
                Verdict(clean, "a", passed: true),
                Verdict(unjudged, "b", passed: true),
                Verdict(unjudged, "c", passed: true, notJudged: true),
                Verdict(noAnswer, "d", passed: true),
            ]
        );
        var byKey = results.Scenarios.ToDictionary(scenario => scenario.Key);

        Assert.True(byKey["clean"].PassedFully);
        Assert.True(byKey["clean"].Checks.Single().PassedFully);
        Assert.False(byKey["unjudged"].PassedFully);
        Assert.Equal([true, false], byKey["unjudged"].Checks.Select(check => check.PassedFully));
        Assert.False(byKey["no-answer"].PassedFully);
        Assert.False(byKey["unchecked"].PassedFully);
    }
}
