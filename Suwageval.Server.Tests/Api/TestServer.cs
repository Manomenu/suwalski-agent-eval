using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Tests.Agents;
using Suwageval.Server.Tests.Evaluation.Runs;

namespace Suwageval.Server.Tests.Api;

/// <summary>The whole server in-process on the test database, and short ways to talk to it in the contract records.</summary>
internal sealed class TestServer(TestDatabase database) : IAsyncDisposable
{
    // Without the run worker: a worker here would take the runs other tests queue on the same database.
    private readonly WebApplicationFactory<Program> app = RunTestApp.Create();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Client
    {
        get
        {
            // Skips (or fails in CI) without a database: the app does not start without one.
            _ = database.Pool;
            return app.CreateClient();
        }
    }

    public Task<HttpResponseMessage> Get(string url) => Client.GetAsync(new Uri(url, UriKind.Relative), Cancellation);

    public Task<HttpResponseMessage> Delete(string url) => Client.DeleteAsync(new Uri(url, UriKind.Relative), Cancellation);

    public Task<HttpResponseMessage> Post(string url, object body) =>
        Client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, ContractJson.Options, Cancellation);

    public Task<HttpResponseMessage> Put(string url, object body) =>
        Client.PutAsJsonAsync(new Uri(url, UriKind.Relative), body, ContractJson.Options, Cancellation);

    /// <summary>A body as written by hand, for requests the contract records cannot express (a typo'd field).</summary>
    public async Task<HttpResponseMessage> PostRaw(string url, string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await Client.PostAsync(new Uri(url, UriKind.Relative), content, Cancellation);
    }

    public static async Task<T> Read<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("Empty body.");

    /// <summary>The names of the fields a 422 answer complains about.</summary>
    public static async Task<IReadOnlyList<string>> ErrorFields(HttpResponseMessage response)
    {
        var problem = await Read<JsonElement>(response);
        return [.. problem.GetProperty("errors").EnumerateObject().Select(field => field.Name)];
    }

    /// <summary>A new agent with a name of its own; the name.</summary>
    public async Task<string> NewAgent()
    {
        var name = AgentStoreTests.UniqueName();
        (await Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://agent.test" })).EnsureSuccessStatusCode();
        return name;
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
