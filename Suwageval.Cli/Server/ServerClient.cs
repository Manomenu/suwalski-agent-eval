using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Server;

/// <summary>The server's endpoints for agents, scenarios, checks, runs and versions, spoken with the contract records. Anything that goes wrong is a <see cref="ServerClientException"/>.</summary>
internal sealed class ServerClient(HttpClient http)
{
    private const int MaxPageSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>The agent, and whether this call created it (otherwise it existed; its endpoint URL is now the given one).</summary>
    public Task<(Agent Agent, bool Created)> PutAgentAsync(AgentDefinition definition, CancellationToken cancellationToken) =>
        ReadAsync(
            () => http.PostAsJsonAsync(new Uri("agents", UriKind.Relative), definition, ContractJson.Options, cancellationToken),
            async response => (await ReadBodyAsync<Agent>(response, cancellationToken), response.StatusCode == HttpStatusCode.Created),
            cancellationToken
        );

    public Task<PutScenariosResponse> PutScenariosAsync(
        string agent,
        IReadOnlyList<ScenarioInput> scenarios,
        CancellationToken cancellationToken
    ) =>
        SendAsync<PutScenariosResponse>(
            () =>
                http.PutAsJsonAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/scenarios", UriKind.Relative),
                    new PutScenariosRequest(scenarios),
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<PutChecksResponse> PutChecksAsync(string agent, IReadOnlyList<CheckInput> checks, CancellationToken cancellationToken) =>
        SendAsync<PutChecksResponse>(
            () =>
                http.PutAsJsonAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/checks", UriKind.Relative),
                    new PutChecksRequest(checks),
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

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

    /// <summary>What a re-check of run <paramref name="runId"/> would do and whether it can start.</summary>
    public Task<RecheckPlan> RecheckPlanAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RecheckPlan>(
            () => http.GetAsync(new Uri($"runs/{runId}/recheck", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>Starts a re-check of run <paramref name="runId"/>: its stored answers judged again by the current checks.</summary>
    public Task<RunCreated> RecheckAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsync(new Uri($"runs/{runId}/recheck", UriKind.Relative), null, cancellationToken),
            cancellationToken
        );

    /// <summary>Asks the server to stop run <paramref name="runId"/>: it answers 202 for any known run, a finished one stays as it is.</summary>
    public Task CancelAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync(() => http.PostAsync(new Uri($"runs/{runId}/cancel", UriKind.Relative), null, cancellationToken), cancellationToken);

    public Task<RunSummary> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunSummary>(() => http.GetAsync(new Uri($"runs/{runId}", UriKind.Relative), cancellationToken), cancellationToken);

    /// <summary>The agent's newest runs, newest first, at most <paramref name="limit"/> of them (asked for in pages the server accepts).</summary>
    public async Task<IReadOnlyList<RunSummary>> RunsAsync(string agent, int limit, CancellationToken cancellationToken)
    {
        var runs = new List<RunSummary>();
        Guid? before = null;
        while (runs.Count < limit)
        {
            var url = $"runs?agent={Uri.EscapeDataString(agent)}&limit={Math.Min(limit - runs.Count, MaxPageSize)}";
            if (before is { } cursor)
            {
                url += $"&before={cursor}";
            }
            var page = await SendAsync<RunList>(() => http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken), cancellationToken);
            runs.AddRange(page.Runs);
            if (page.Next is not { } next)
            {
                break;
            }
            before = next;
        }
        return runs;
    }

    /// <summary>The agent's unstable checks: passed in some finished runs of one version and failed in others, most often failing first.</summary>
    public Task<IReadOnlyList<UnstableCheck>> UnstableChecksAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<UnstableCheck>>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/unstable-checks", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>The scenarios where the agent gave no answer, per version, the highest share of samples first.</summary>
    public Task<IReadOnlyList<AgentError>> AgentErrorsAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<AgentError>>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/agent-errors", UriKind.Relative), cancellationToken),
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

    /// <summary>One version of the agent with its contract; the server answers 404 for a version the agent does not have.</summary>
    public Task<AgentVersionDetail> VersionAsync(string agent, Guid versionId, CancellationToken cancellationToken) =>
        SendAsync<AgentVersionDetail>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/{versionId}", UriKind.Relative), cancellationToken),
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
        await ReadAsync(send, response => ReadBodyAsync<T>(response, cancellationToken), cancellationToken);

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken)
        ?? throw new ServerClientException("The server's answer was empty.");

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
                throw new ServerClientException(Describe(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken)));
            }
            return await read(response);
        }
        catch (HttpRequestException error)
        {
            throw new ServerClientException($"The server could not be reached: {error.Message}");
        }
        catch (JsonException error)
        {
            throw new ServerClientException($"The server's answer could not be read: {error.Message}");
        }
    }

    /// <summary>The Problem Details of the server in words: its detail, or every field error of a validation problem.</summary>
    private static string Describe(HttpStatusCode status, string body)
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
