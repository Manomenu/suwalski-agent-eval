using System.Diagnostics.CodeAnalysis;
using System.Text;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.LanguageModels;

namespace Suwageval.Server.Evaluation.Simulation;

/// <summary>
/// Drafts <c>continuation.simulatedUser</c> from a real conversation, for a scenario that copies its start and simulates the rest: a
/// model reads the whole conversation as the user saw it and describes that user, so the simulation can go the way the real one went. A
/// person reviews the text before it goes into a scenario. When no model can write it, the draft is the user's own messages with a line
/// to rewrite, never an error.
/// </summary>
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

    private const string JudgeEntry = "judge";

    /// <summary>The entry under <c>Models</c> that describes the user: <c>describer</c> when configured, else the judge's <c>judge</c>; null when neither is.</summary>
    public string? ModelEntry =>
        models.Has(DescriberEntry) ? DescriberEntry
        : models.Has(JudgeEntry) ? JudgeEntry
        : null;

    /// <summary>Whether a model of its own is configured (<c>Models:describer</c>), not the judge's.</summary>
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
            return Fallback(conversation, "no describer or judge model is configured");
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

    /// <summary>The conversation as the user saw it: their messages and the agent's text, one line each.</summary>
    public static string Prompt(IReadOnlyList<ConversationItem> conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var prompt = new StringBuilder().AppendLine("The conversation:");
        foreach (var item in conversation)
        {
            switch (item)
            {
                case UserMessage user:
                    prompt.Append("User: ").AppendLine(user.Text.ReplaceLineEndings(" "));
                    break;
                case AssistantMessage { Text.Length: > 0 } said:
                    prompt.Append("Agent: ").AppendLine(said.Text.ReplaceLineEndings(" "));
                    break;
                default:
                    break;
            }
        }
        return prompt.AppendLine().Append("Describe the user.").ToString();
    }

    /// <summary>The draft without a model: the user's messages, for the person to turn into a description.</summary>
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
