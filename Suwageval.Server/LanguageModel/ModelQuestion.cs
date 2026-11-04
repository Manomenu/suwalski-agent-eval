using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Suwageval.AgentSdk;

namespace Suwageval.Server.LanguageModel;

internal static class ModelQuestion
{
    private const int MaxErrorLength = 300;

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

    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxErrorLength ? line : line[..MaxErrorLength] + "…";
    }
}
