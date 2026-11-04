using System.Diagnostics.CodeAnalysis;
using System.Text;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.LanguageModel;

namespace Suwageval.Server.Evaluation.Simulation;

internal sealed class UserDescriber(ModelClients models)
{
    private const string Instructions = """
        You read a real conversation between a user and an AI agent. Describe that user so that a model can play them in a test of the
        agent: their goal, their temperament, their situation, what they know (numbers, names, dates they mention), how they react when
        refused, when they would leave, and the language they write in. The goal and the behaviour must match how the conversation went,
        to its end, so that a simulation of this user can go the same way. Write it in the second person ("You are …"), in English, in at
        most six sentences, from what the user wrote; never describe the agent, never invent facts the conversation does not give.
        Answer with JSON only: description (the text).
        """;

    private const string DescriberEntry = "describer";

    public string? ModelEntry => models.EntryFor(DescriberEntry);

    public bool HasOwnModel => models.Has(DescriberEntry);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Whatever stops the model answering leaves the plain draft, with the reason."
    )]
    public async Task<UserDescription> DescribeAsync(IReadOnlyList<ConversationItem> conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (ModelEntry is not { } model)
        {
            return Fallback(conversation, "no describer or fallback model is configured");
        }
        string text;
        try
        {
            text = await ModelQuestion.AskAsync<DescribedUser>(
                models.Get(model),
                "user-describer",
                Instructions,
                Prompt(conversation),
                temperature: 0,
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return Fallback(conversation, $"the model '{model}' could not be asked ({ModelQuestion.Shorten(error.Message)})");
        }
        return ModelQuestion.Read<DescribedUser>(text) is { Description: { } description } && !string.IsNullOrWhiteSpace(description)
            ? new UserDescription(description.Trim())
            : Fallback(conversation, $"the model's answer is not the JSON asked for ({ModelQuestion.Shorten(text)})");
    }

    public static string Prompt(IReadOnlyList<ConversationItem> conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var prompt = new StringBuilder().Append("The conversation:\n");
        foreach (var item in conversation)
        {
            switch (item)
            {
                case UserMessage user:
                    prompt.Append("User: ").Append(user.Text.ReplaceLineEndings(" ")).Append('\n');
                    break;
                case AssistantMessage { Text.Length: > 0 } said:
                    prompt.Append("Agent: ").Append(said.Text.ReplaceLineEndings(" ")).Append('\n');
                    break;
                default:
                    break;
            }
        }
        return prompt.Append("\nDescribe the user.").ToString();
    }

    public static UserDescription Fallback(IReadOnlyList<ConversationItem> conversation, string why)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var said = conversation.OfType<UserMessage>().Select(message => $"- {message.Text.ReplaceLineEndings(" ")}");
        return new UserDescription(
            $"Rewrite: describe this user (goal, temperament, what they know, the language they write in). In the real conversation they wrote:\n{string.Join('\n', said)}",
            $"The simulated user was not described by a model: {why}."
        );
    }
}
