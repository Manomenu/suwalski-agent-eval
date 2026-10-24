using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Suwageval.AgentSdk;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Server.LanguageModels;

namespace Suwageval.Server.Evaluation.Simulation;

/// <summary>
/// Drafts <c>continuation.simulatedUser</c> from a real conversation, for a scenario that replays its start and simulates the rest: the
/// simulator's model reads the conversation as the user saw it and describes that user. A person reviews the text before it goes into a
/// scenario. When no model can write it, the draft is the user's own messages with a line to rewrite, never an error.
/// </summary>
internal sealed class UserDescriber(UserSimulator simulator, ModelClients models)
{
    private const string Instructions = """
        You read a real conversation between a user and an AI agent. Describe that user so that a model can play them in a test of the
        agent: their goal, their temperament, their situation, what they know (numbers, names, dates they mention), how they react when
        refused, when they would leave, and the language they write in. Write it in the second person ("You are …"), in English, in at
        most six sentences, from what the user wrote; never describe the agent, never invent facts the conversation does not give.
        Answer with JSON only: description (the text).
        """;

    private const int MaxErrorLength = 300;

    private static readonly JsonSerializerOptions Json = ContractJson.Options;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Whatever stops the model answering leaves the plain draft, with the reason."
    )]
    public async Task<UserDescription> DescribeAsync(IReadOnlyList<ConversationItem> conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (simulator.ModelEntry is not { } model)
        {
            return Fallback(conversation, "no simulator model is configured");
        }
        string text;
        try
        {
            var agent = new ChatClientAgent(
                models.Get(model),
                new ChatClientAgentOptions
                {
                    Name = "user-describer",
                    ChatOptions = new ChatOptions
                    {
                        Instructions = Instructions,
                        Temperature = 0,
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<DescribedUser>(Json),
                    },
                }
            );
            text = (await agent.RunAsync(Prompt(conversation), cancellationToken: cancellationToken)).Text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return Fallback(conversation, $"the simulator model could not be asked ({Shorten(error.Message)})");
        }
        return Read(text) is { Description: { } description } && !string.IsNullOrWhiteSpace(description)
            ? new UserDescription(description.Trim())
            : Fallback(conversation, $"the simulator model's answer is not the JSON asked for ({Shorten(text)})");
    }

    /// <summary>The answer as asked for, or null when it is anything else.</summary>
    public static DescribedUser? Read(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<DescribedUser>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
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
            $"The description was not drafted by a model: {why}."
        );
    }

    private static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxErrorLength ? line : line[..MaxErrorLength] + "…";
    }
}
