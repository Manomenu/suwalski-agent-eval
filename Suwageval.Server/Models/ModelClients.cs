using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Suwageval.Server.Models;

/// <summary>
/// The chat client of every configured model, built once: the same instance — and so the same pool of slots — for everyone
/// who asks for a model by name.
/// </summary>
/// <remarks><paramref name="create"/> makes the unlimited client of one model; the tests hand in one that answers from recorded text.</remarks>
internal sealed class ModelClients(ModelsOptions options, Func<ModelOptions, IChatClient> create) : IDisposable
{
    private readonly Dictionary<string, LimitedChatClient> clients = options.ToDictionary(
        model => model.Key,
        model => Limited(model.Value, create(model.Value)),
        StringComparer.OrdinalIgnoreCase
    );

    public bool Has(string name) => clients.ContainsKey(name);

    /// <summary>The model's own name on its server (<see cref="ModelOptions.Model"/>) of the model called <paramref name="name"/>; null when it is not configured.</summary>
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

    /// <summary>The client of an OpenAI-compatible server.</summary>
    public static IChatClient OpenAiCompatible(ModelOptions model)
    {
        ArgumentNullException.ThrowIfNull(model);
        // The OpenAI client wants a key to build, so a server that asks for none gets a placeholder.
        var apiKey = string.IsNullOrEmpty(model.ApiKey) ? "none" : model.ApiKey;
        var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(model.BaseUrl) });
        return openAi.GetChatClient(model.Model).AsIChatClient();
    }

    private static LimitedChatClient Limited(ModelOptions model, IChatClient client) =>
        new(client, model.MaxConcurrency, TimeSpan.FromSeconds(model.TimeoutSeconds));
}
