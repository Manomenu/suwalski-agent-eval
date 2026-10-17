using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Suwageval.Contracts;
using Suwageval.Contracts.Conversations;
using Suwageval.Server.Checks;

namespace Suwageval.Server.Judges;

/// <summary>What the two judges — the <c>judge</c> check (<see cref="Judge"/>) and <c>$judge</c> matching (<see cref="ArgumentJudge"/>) — share: one question to the model, and the "not judged" verdict.</summary>
internal static class JudgeModel
{
    private const int MaxErrorLength = 300;

    /// <summary>Asks the model one question, temperature 0, answer as JSON following <typeparamref name="TAnswer"/>'s schema; the text of its answer.</summary>
    public static async Task<string> AskAsync<TAnswer>(
        IChatClient client,
        string name,
        string instructions,
        string prompt,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        var agent = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Name = name,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                    Temperature = 0,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<TAnswer>(ContractJson.Options),
                },
            }
        );
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    /// <summary>The verdict of a check the model could not judge: failed, flagged, with the reason after the "Not judged:" prefix.</summary>
    public static CheckVerdict NotJudged(IReadOnlyList<ConversationItem> evidence, string why, string? modelName = null) =>
        new(false, evidence, $"Not judged: {why}.", NotJudged: true, ModelName: modelName);

    public static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxErrorLength ? line : line[..MaxErrorLength] + "…";
    }
}
