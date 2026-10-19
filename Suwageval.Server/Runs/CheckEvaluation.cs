using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Checks;
using Suwageval.Server.Judges;

namespace Suwageval.Server.Runs;

/// <summary>How a check judges the output of one sample — shared by a run, which has just called the agent, and a re-check, which has not.</summary>
internal static class CheckEvaluation
{
    public static async Task<NewCheckResult> EvaluateAsync(
        Judge judge,
        ArgumentJudge argumentJudge,
        CheckRow row,
        CheckInput input,
        ScenarioContent scenario,
        Trace trace,
        CancellationToken cancellationToken
    )
    {
        var verdict = input switch
        {
            JudgeCheck judgeCheck => await judge.JudgeAsync(judgeCheck, scenario.Conversation, trace, cancellationToken),
            _ when CheckEvaluator.JudgedByModel(input) => await argumentJudge.JudgeAsync(input, trace, cancellationToken),
            _ => CheckEvaluator.Evaluate(input, trace),
        };
        return new NewCheckResult(
            row.Id,
            row.Revision,
            verdict.Passed,
            JsonSerializer.Serialize(verdict.Evidence, ContractJson.Options),
            verdict.Message,
            verdict.NotJudged,
            verdict.ModelName
        );
    }

    /// <summary>The verdict of a check on a sample that produced nothing: failed, and says why it was not evaluated.</summary>
    public static NewCheckResult NotEvaluated(CheckRow row, string reason) =>
        new(row.Id, row.Revision, false, "[]", $"Not evaluated: {reason}");
}
