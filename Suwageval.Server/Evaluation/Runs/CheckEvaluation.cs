using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Evaluation.Checks;
using Suwageval.Server.Judges;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Evaluation.Runs;

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

    public static NewCheckResult NotEvaluated(CheckRow row, string reason) =>
        new(row.Id, row.Revision, false, "[]", $"Not evaluated: {reason}");
}
