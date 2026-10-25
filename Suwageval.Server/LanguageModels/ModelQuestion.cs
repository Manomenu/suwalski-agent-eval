using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Suwageval.AgentSdk;

namespace Suwageval.Server.LanguageModels;

/// <summary>
/// One question to a model with its answer as JSON — what the judges, the simulated user and the user describer share: the call, reading
/// the answer (anything but the JSON asked for is null, never an exception) and the short form of a failure for a message.
/// </summary>
internal static class ModelQuestion
{
    private const int MaxErrorLength = 300;

    /// <summary>Asks the model one question, the answer as JSON following <typeparamref name="TAnswer"/>'s schema; the text of its answer.</summary>
    public static async Task<string> AskAsync<TAnswer>(
        IChatClient client,
        string name,
        string instructions,
        string prompt,
        float temperature,
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
                    Temperature = temperature,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<TAnswer>(ContractJson.Options),
                },
            }
        );
        return (await agent.RunAsync(prompt, cancellationToken: cancellationToken)).Text;
    }

    /// <summary>The answer as asked for, or null when it is anything else.</summary>
    public static TAnswer? Read<TAnswer>(string text)
        where TAnswer : class
    {
        try
        {
            return JsonSerializer.Deserialize<TAnswer>(text, ContractJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A model's answer or error on one line, cut to fit in a message.</summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxErrorLength ? line : line[..MaxErrorLength] + "…";
    }
}
