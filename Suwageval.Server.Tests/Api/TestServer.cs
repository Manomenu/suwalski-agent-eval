using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Suwageval.AgentSdk;
using Suwageval.Contracts.Agents;
using Suwageval.Server.Tests.Agents;

namespace Suwageval.Server.Tests.Api;

internal sealed class TestServer(TestDatabase database, ClassServer classServer)
{
    private readonly WebApplicationFactory<Program> app = classServer.App;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Client
    {
        get
        {
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

    public async Task<HttpResponseMessage> PostRaw(string url, string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await Client.PostAsync(new Uri(url, UriKind.Relative), content, Cancellation);
    }

    public static async Task<T> Read<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, Cancellation)
        ?? throw new InvalidOperationException("Empty body.");

    public static async Task<IReadOnlyList<string>> ErrorFields(HttpResponseMessage response)
    {
        var problem = await Read<JsonElement>(response);
        return [.. problem.GetProperty("errors").EnumerateObject().Select(field => field.Name)];
    }

    public async Task<string> NewAgent()
    {
        var name = AgentStoreTests.UniqueName();
        (await Post("/agents", new AgentDefinition { Name = name, EndpointUrl = "http://agent.test" })).EnsureSuccessStatusCode();
        return name;
    }
}
