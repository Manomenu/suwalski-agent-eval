using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Server.Agents;
using Suwageval.Server.Judges;
using Suwageval.Server.Monitoring.Conversations;
using Suwageval.Server.Verdicts;

namespace Suwageval.Server.Monitoring.Checking;

internal sealed class ConversationChecker(CheckingStore checking, ConversationStore conversations, Judge judge, ArgumentJudge argumentJudge)
{
    public async Task<bool> CheckAsync(Guid conversationId, Guid agentId, CancellationToken cancellationToken)
    {
        var turns = await conversations.TurnsAsync(conversationId, cancellationToken);
        var trace = new Trace([
            .. turns.Select(turn =>
                (IReadOnlyList<ConversationItem>)(JsonSerializer.Deserialize<ConversationItem[]>(turn.Items, ContractContent.Lenient) ?? [])
            ),
        ]);
        var verdicts = new List<CheckingStore.Verdict>();
        foreach (var check in await checking.ChecksAsync(agentId, cancellationToken))
        {
            var input =
                JsonSerializer.Deserialize<CheckInput>(check.Content, ContractContent.Lenient)
                ?? throw new InvalidOperationException("A stored check is empty.");
            var verdict = input switch
            {
                JudgeCheck judgeCheck => await judge.JudgeAsync(judgeCheck, [], trace, cancellationToken),
                _ when CheckEvaluator.JudgedByModel(input) => await argumentJudge.JudgeAsync(input, trace, cancellationToken),
                _ => CheckEvaluator.Evaluate(input, trace),
            };
            verdicts.Add(
                new CheckingStore.Verdict(
                    check.Id,
                    check.Revision,
                    verdict.Passed,
                    verdict.NotJudged,
                    verdict.Message,
                    // Why: a judge check's evidence is the whole conversation, already on its page, so it is stored without it
                    JsonSerializer.Serialize(input is JudgeCheck ? [] : verdict.Evidence, ContractJson.Options),
                    verdict.ModelName
                )
            );
        }
        return await checking.StoreAsync(conversationId, verdicts, cancellationToken);
    }
}
