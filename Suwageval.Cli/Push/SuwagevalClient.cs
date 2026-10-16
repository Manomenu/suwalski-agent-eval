using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Push;

/// <summary>The server's HTTP API, as the CLI uses it. Bodies and answers are the contract records.</summary>
internal sealed class SuwagevalClient(HttpClient http)
{
    /// <summary>The agent, and whether this call created it (otherwise it existed; its endpoint URL is now the given one).</summary>
    public async Task<(Agent Agent, bool Created)> PutAgentAsync(AgentDefinition definition, CancellationToken cancellationToken)
    {
        var response = await SendAsync(client => client.PostAsJsonAsync("agents", definition, ContractJson.Options, cancellationToken));
        return (await ReadAsync<Agent>(response, cancellationToken), response.StatusCode == HttpStatusCode.Created);
    }

    public async Task<PutScenariosResponse> PutScenariosAsync(
        string agent,
        IReadOnlyList<ScenarioInput> scenarios,
        CancellationToken cancellationToken
    )
    {
        var response = await SendAsync(client =>
            client.PutAsJsonAsync(
                $"agents/{Uri.EscapeDataString(agent)}/scenarios",
                new PutScenariosRequest(scenarios),
                ContractJson.Options,
                cancellationToken
            )
        );
        return await ReadAsync<PutScenariosResponse>(response, cancellationToken);
    }

    public async Task<PutChecksResponse> PutChecksAsync(string agent, IReadOnlyList<CheckInput> checks, CancellationToken cancellationToken)
    {
        var response = await SendAsync(client =>
            client.PutAsJsonAsync(
                $"agents/{Uri.EscapeDataString(agent)}/checks",
                new PutChecksRequest(checks),
                ContractJson.Options,
                cancellationToken
            )
        );
        return await ReadAsync<PutChecksResponse>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send(http);
        }
        catch (HttpRequestException error)
        {
            throw new ServerException($"Cannot reach the server at {http.BaseAddress}: {error.Message}");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ServerException(await DescribeAsync(response, cancellationToken));
            }
            return await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken)
                ?? throw new ServerException("The server sent an empty answer.");
        }
    }

    /// <summary>The Problem Details of an error answer in one message: title, detail and the fields that were wrong.</summary>
    private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
        ProblemBody? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonSerializerOptions.Web, cancellationToken);
        }
        catch (JsonException)
        {
            // Not Problem Details (a proxy's page, say): the status says enough.
        }
        if (problem is null)
        {
            return $"The server answered {status}.";
        }
        var lines = new List<string> { $"The server answered {status}: {problem.Detail ?? problem.Title ?? "no details"}" };
        foreach (var (field, messages) in problem.Errors ?? [])
        {
            lines.AddRange(messages.Select(message => $"  {field}: {message}"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private sealed record ProblemBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
