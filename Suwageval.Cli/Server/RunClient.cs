using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Server;

/// <summary>The server's run endpoints and the agent version ones, spoken with the contract records. Anything that goes wrong is a <see cref="RunClientException"/>.</summary>
internal sealed class RunClient(HttpClient http)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public Task<RunCreated> CreateAsync(RunRequest request, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsJsonAsync(new Uri("runs", UriKind.Relative), request, ContractJson.Options, cancellationToken),
            cancellationToken
        );

    /// <summary>A new run with the agent, tag and endpoint of run <paramref name="runId"/> (the endpoint unless another is given).</summary>
    public Task<RunCreated> RerunAsync(Guid runId, string? endpointUrl, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () =>
                http.PostAsJsonAsync(
                    new Uri($"runs/{runId}/rerun", UriKind.Relative),
                    new RerunRequest { EndpointUrl = endpointUrl },
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<RunSummary> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunSummary>(() => http.GetAsync(new Uri($"runs/{runId}", UriKind.Relative), cancellationToken), cancellationToken);

    /// <summary>The agent's runs, newest first.</summary>
    public Task<IReadOnlyList<RunSummary>> RunsAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<RunSummary>>(
            () => http.GetAsync(new Uri($"runs?agent={Uri.EscapeDataString(agent)}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<RunResults> ResultsAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunResults>(
            () => http.GetAsync(new Uri($"runs/{runId}/results", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>Asks for the run until it is done, failed or cancelled.</summary>
    public async Task WaitForEndAsync(Guid runId, CancellationToken cancellationToken)
    {
        while ((await GetAsync(runId, cancellationToken)).Status is RunStatus.Queued or RunStatus.Running)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public Task<IReadOnlyList<AgentVersion>> VersionsAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<AgentVersion>>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/versions", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task ArchiveVersionsAsync(string agent, ArchiveVersionsRequest request, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.PostAsJsonAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/archive", UriKind.Relative),
                    request,
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken) =>
        await ReadAsync(
            send,
            async response =>
                await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken)
                ?? throw new RunClientException("The server's answer was empty."),
            cancellationToken
        );

    /// <summary>For an answer with no body.</summary>
    private static async Task SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken) =>
        await ReadAsync(send, _ => Task.FromResult(0), cancellationToken);

    private static async Task<T> ReadAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, Task<T>> read,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                throw new RunClientException(Describe(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken)));
            }
            return await read(response);
        }
        catch (HttpRequestException error)
        {
            throw new RunClientException($"The server could not be reached: {error.Message}");
        }
        catch (JsonException error)
        {
            throw new RunClientException($"The server's answer could not be read: {error.Message}");
        }
    }

    /// <summary>The Problem Details of the server in words: its detail, or every field error of a validation problem.</summary>
    private static string Describe(System.Net.HttpStatusCode status, string body)
    {
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"The server answered {(int)status} {status}");
        try
        {
            using var problem = JsonDocument.Parse(body);
            var root = problem.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        text.Append(
                            CultureInfo.InvariantCulture,
                            $": {field.Name}: {string.Join(" ", field.Value.EnumerateArray().Select(message => message.GetString()))}"
                        );
                    }
                }
                else if (root.TryGetProperty("mismatches", out var mismatches) && mismatches.ValueKind == JsonValueKind.Array)
                {
                    text.Append(": the agent's version does not fit its scenarios:");
                    foreach (var mismatch in mismatches.EnumerateArray())
                    {
                        text.Append("\n  - ").Append(mismatch.GetString());
                    }
                }
                else if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                {
                    text.Append(": ").Append(detail.GetString());
                }
            }
        }
        catch (JsonException)
        {
            // Not Problem Details (a proxy's page, say): the status alone has to do.
        }
        return text.ToString();
    }
}
