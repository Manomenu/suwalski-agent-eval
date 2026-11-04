using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Suwageval.Server.LanguageModel;

// Why: one instance per model, so everyone who names a model shares its pool of slots
internal sealed class ModelClients(ModelsOptions options, Func<ModelOptions, IChatClient> create) : IDisposable
{
    private readonly Dictionary<string, LimitedChatClient> clients = options.ToDictionary(
        model => model.Key,
        model => Limited(model.Value, create(model.Value)),
        StringComparer.OrdinalIgnoreCase
    );

    public const string FallbackEntry = "fallback";

    public bool Has(string name) => clients.ContainsKey(name);

    public string? EntryFor(string role) =>
        Has(role) ? role
        : Has(FallbackEntry) ? FallbackEntry
        : null;

    public string? ModelNameOf(string name) => options.TryGetValue(name, out var model) ? model.Model : null;

    public IChatClient Get(string name) =>
        clients.TryGetValue(name, out var client) ? client : throw new InvalidOperationException($"No model '{name}' is configured.");

    public void Dispose()
    {
        foreach (var client in clients.Values)
        {
            client.Dispose();
        }
    }

    public static IChatClient OpenAiCompatible(ModelOptions model)
    {
        ArgumentNullException.ThrowIfNull(model);
        // Why: the OpenAI client wants a key to build, so a server that asks for none gets a placeholder
        var apiKey = string.IsNullOrEmpty(model.ApiKey) ? "none" : model.ApiKey;
        var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(model.BaseUrl) });
        return openAi.GetChatClient(model.Model).AsIChatClient();
    }

    private static LimitedChatClient Limited(ModelOptions model, IChatClient client) =>
        new(client, model.MaxConcurrency, TimeSpan.FromSeconds(model.TimeoutSeconds));
}
