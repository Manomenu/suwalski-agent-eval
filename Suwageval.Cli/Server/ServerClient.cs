using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwageval.AgentSdk;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Monitoring;
using Suwageval.Contracts.Runs;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Server;

internal sealed class ServerClient(HttpClient http)
{
    private const int MaxPageSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

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
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/scenarios", UriKind.Relative),
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
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/checks", UriKind.Relative),
                    new PutChecksRequest(checks),
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<RunCreated> CreateAsync(RunRequest request, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsJsonAsync(new Uri("eval/runs", UriKind.Relative), request, ContractJson.Options, cancellationToken),
            cancellationToken
        );

    public Task<RecheckPlan> RecheckPlanAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RecheckPlan>(
            () => http.GetAsync(new Uri($"eval/runs/{runId}/recheck", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<RunCreated> RecheckAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsync(new Uri($"eval/runs/{runId}/recheck", UriKind.Relative), null, cancellationToken),
            cancellationToken
        );

    public Task CancelAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync(() => http.PostAsync(new Uri($"eval/runs/{runId}/cancel", UriKind.Relative), null, cancellationToken), cancellationToken);

    public Task SetNoteAsync(Guid runId, string? note, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.PutAsJsonAsync(
                    new Uri($"eval/runs/{runId}/note", UriKind.Relative),
                    new NoteRequest { Note = note },
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task SetGroupNoteAsync(string agent, string group, string? note, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.PutAsJsonAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/run-groups/{Uri.EscapeDataString(group)}/note", UriKind.Relative),
                    new NoteRequest { Note = note },
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<RunSummary> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunSummary>(() => http.GetAsync(new Uri($"eval/runs/{runId}", UriKind.Relative), cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<RunSummary>> RunsAsync(
        string agent,
        int limit,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? versions = null,
        bool referenceOnly = false,
        string? group = null,
        RunGrouping grouping = RunGrouping.All,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? dataVersions = null
    )
    {
        var runs = new List<RunSummary>();
        // Why: the server pages by number, so changing the page size between requests would skip or repeat runs
        var size = referenceOnly ? MaxPageSize : Math.Min(limit, MaxPageSize);
        for (var page = 1; runs.Count < limit; page++)
        {
            var url = $"eval/runs?agent={Uri.EscapeDataString(agent)}&page={page}&pageSize={size}";
            foreach (var version in versions ?? [])
            {
                url += $"&version={version}";
            }
            if (group is not null)
            {
                url += $"&group={Uri.EscapeDataString(group)}";
            }
            if (grouping != RunGrouping.All)
            {
                url += $"&grouping={grouping}";
            }
            foreach (var tag in tags ?? [])
            {
                url += $"&tag={Uri.EscapeDataString(tag)}";
            }
            foreach (var data in dataVersions ?? [])
            {
                url += $"&dataVersion={Uri.EscapeDataString(data)}";
            }
            var list = await SendAsync<RunList>(() => http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken), cancellationToken);
            runs.AddRange(referenceOnly ? list.Runs.Where(run => run.Reference) : list.Runs);
            if (page * size >= list.TotalCount)
            {
                break;
            }
        }
        return runs.Count > limit ? runs[..limit] : runs;
    }

    public Task<IReadOnlyList<RunGroupSummary>> RunGroupsAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<RunGroupSummary>>(
            () => http.GetAsync(new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/run-groups", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task ArchiveRunGroupsAsync(string agent, ArchiveRunGroupsRequest request, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.PostAsJsonAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/run-groups/archive", UriKind.Relative),
                    request,
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<RunGroupDeletion> RunGroupDeletionAsync(string agent, string group, CancellationToken cancellationToken) =>
        SendAsync<RunGroupDeletion>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"eval/agents/{Uri.EscapeDataString(agent)}/run-groups/{Uri.EscapeDataString(group)}/deletion",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task DeleteRunGroupAsync(string agent, string group, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.DeleteAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/run-groups/{Uri.EscapeDataString(group)}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<AgentDetail> AgentAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<AgentDetail>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public async Task<ConversationList> ConversationsAsync(
        string agent,
        int limit,
        string? environment,
        IReadOnlyList<Guid> versions,
        ConversationState? state,
        CancellationToken cancellationToken,
        string? failedCheck = null
    )
    {
        var url = $"monitoring/agents/{Uri.EscapeDataString(agent)}/conversations?pageSize={Math.Min(limit, MaxPageSize)}";
        if (environment is not null)
        {
            url += $"&environment={Uri.EscapeDataString(environment)}";
        }
        foreach (var version in versions)
        {
            url += $"&version={version}";
        }
        if (state is not null)
        {
            url += $"&state={state}";
        }
        if (failedCheck is not null)
        {
            url += $"&failedCheck={Uri.EscapeDataString(failedCheck)}";
        }
        return await SendAsync<ConversationList>(() => http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken), cancellationToken);
    }

    public Task<PutChecksResponse> PutMonitoringChecksAsync(
        string agent,
        IReadOnlyList<CheckInput> checks,
        CancellationToken cancellationToken
    ) =>
        SendAsync<PutChecksResponse>(
            () =>
                http.PutAsJsonAsync(
                    new Uri($"monitoring/agents/{Uri.EscapeDataString(agent)}/checks", UriKind.Relative),
                    new PutChecksRequest(checks),
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<MonitoringCheck>> MonitoringChecksAsync(
        string agent,
        string? environment,
        CancellationToken cancellationToken
    ) =>
        SendAsync<IReadOnlyList<MonitoringCheck>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"monitoring/agents/{Uri.EscapeDataString(agent)}/checks{(environment is null ? "" : $"?environment={Uri.EscapeDataString(environment)}")}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<MonitoringTrend> MonitoringTrendAsync(string agent, int days, string? environment, CancellationToken cancellationToken) =>
        SendAsync<MonitoringTrend>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"monitoring/agents/{Uri.EscapeDataString(agent)}/trend?days={days}{(environment is null ? "" : $"&environment={Uri.EscapeDataString(environment)}")}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<ScenarioDraft> ScenarioDraftAsync(
        Guid conversationId,
        int? upToUser,
        int? upToAgent,
        bool simulate,
        int? turns,
        CancellationToken cancellationToken
    )
    {
        List<string> query = [];
        if (upToUser is { } user)
        {
            query.Add($"upToUserMessage={user.ToString(CultureInfo.InvariantCulture)}");
        }
        if (upToAgent is { } agent)
        {
            query.Add($"upToAgentMessage={agent.ToString(CultureInfo.InvariantCulture)}");
        }
        if (simulate)
        {
            query.Add("simulate=true");
        }
        if (turns is { } count)
        {
            query.Add($"turns={count.ToString(CultureInfo.InvariantCulture)}");
        }
        return SendAsync<ScenarioDraft>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"monitoring/conversations/{conversationId}/scenario" + (query.Count == 0 ? "" : "?" + string.Join('&', query)),
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );
    }

    public Task<ConversationDetail> ConversationAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<ConversationDetail>(
            () => http.GetAsync(new Uri($"monitoring/conversations/{id}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<IReadOnlyList<Agent>> AgentsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<Agent>>(() => http.GetAsync(new Uri("agents", UriKind.Relative), cancellationToken), cancellationToken);

    public Task<IReadOnlyList<ScenarioSummary>> ScenariosAsync(string agent, string? tag, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ScenarioSummary>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"eval/agents/{Uri.EscapeDataString(agent)}/scenarios{(tag is null ? "" : $"?tag={Uri.EscapeDataString(tag)}")}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<ScenarioDetail> ScenarioAsync(string agent, string key, CancellationToken cancellationToken) =>
        SendAsync<ScenarioDetail>(
            () =>
                http.GetAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/scenarios/{Uri.EscapeDataString(key)}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<ScenarioRevision>> ScenarioRevisionsAsync(string agent, string key, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ScenarioRevision>>(
            () =>
                http.GetAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/scenarios/{Uri.EscapeDataString(key)}/revisions", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<CheckDetail>> ChecksAsync(string agent, string? scenario, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CheckDetail>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"eval/agents/{Uri.EscapeDataString(agent)}/checks{(scenario is null ? "" : $"?scenario={Uri.EscapeDataString(scenario)}")}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<RunPassRates>> PassRatesAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<RunPassRates>>(
            () => http.GetAsync(new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/pass-rates", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<IReadOnlyList<UnstableCheck>> UnstableChecksAsync(
        string agent,
        IEnumerable<Guid> versions,
        IEnumerable<string> dataVersions,
        CancellationToken cancellationToken
    ) =>
        SendAsync<IReadOnlyList<UnstableCheck>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"eval/agents/{Uri.EscapeDataString(agent)}/unstable-checks{FilterQuery(versions, dataVersions)}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<AgentError>> AgentErrorsAsync(
        string agent,
        IEnumerable<Guid> versions,
        IEnumerable<string> dataVersions,
        CancellationToken cancellationToken
    ) =>
        SendAsync<IReadOnlyList<AgentError>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"eval/agents/{Uri.EscapeDataString(agent)}/agent-errors{FilterQuery(versions, dataVersions)}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

    private static string FilterQuery(IEnumerable<Guid> versions, IEnumerable<string> dataVersions)
    {
        var query = string.Join(
            '&',
            versions.Select(id => $"version={id}").Concat(dataVersions.Select(data => $"dataVersion={Uri.EscapeDataString(data)}"))
        );
        return string.IsNullOrEmpty(query) ? "" : $"?{query}";
    }

    public Task<RunResults> ResultsAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunResults>(
            () => http.GetAsync(new Uri($"eval/runs/{runId}/results", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public async Task WaitForEndAsync(Guid runId, CancellationToken cancellationToken)
    {
        while ((await GetAsync(runId, cancellationToken)).Status is RunStatus.Queued or RunStatus.Running)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public Task<RunComparison> CompareAsync(
        IReadOnlyCollection<Guid> runIds,
        string? group,
        string? agent,
        Guid? baseline,
        CancellationToken cancellationToken
    )
    {
        var query = group is null
            ? string.Join('&', runIds.Select(id => $"runs={id}"))
            : $"group={Uri.EscapeDataString(group)}{(agent is null ? "" : $"&agent={Uri.EscapeDataString(agent)}")}";
        return SendAsync<RunComparison>(
            () =>
                http.GetAsync(
                    new Uri($"eval/runs/compare?{query}{(baseline is null ? "" : $"&baseline={baseline}")}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );
    }

    public Task<VersionDiff> VersionDiffAsync(string agent, Guid from, Guid to, CancellationToken cancellationToken) =>
        SendAsync<VersionDiff>(
            () =>
                http.GetAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/diff?from={from}&to={to}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task<IReadOnlyList<AgentVersion>> VersionsAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<AgentVersion>>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/versions", UriKind.Relative), cancellationToken),
            cancellationToken
        );

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

    public Task<AgentDeletion> AgentDeletionAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<AgentDeletion>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/deletion", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task DeleteAgentAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync(
            () => http.DeleteAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<VersionDeletion> VersionDeletionAsync(string agent, Guid versionId, CancellationToken cancellationToken) =>
        SendAsync<VersionDeletion>(
            () =>
                http.GetAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/{versionId}/deletion", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    public Task DeleteVersionAsync(string agent, Guid versionId, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.DeleteAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/{versionId}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken) =>
        await ReadAsync(send, response => ReadBodyAsync<T>(response, cancellationToken), cancellationToken);

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(ServerAnswers.Options, cancellationToken)
        ?? throw new ServerClientException("The server's answer was empty.");

    private static async Task SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken) =>
        await ReadAsync(send, _ => Task.FromResult(0), cancellationToken);

    private static async Task<T> ReadAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, Task<T>> read,
        CancellationToken cancellationToken
    )
    {
        string? serverVersion = null;
        try
        {
            using var response = await send();
            serverVersion = response.Headers.TryGetValues(ServerVersion.Header, out var versions) ? versions.FirstOrDefault() : null;
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new ServerClientException(Describe(response.StatusCode, body), FeatureOf(body));
            }
            return await read(response);
        }
        catch (HttpRequestException error)
        {
            throw new ServerClientException($"The server could not be reached: {error.Message}");
        }
        catch (JsonException error)
        {
            throw new ServerClientException(ServerAnswers.Unreadable(error.Message, serverVersion));
        }
    }

    private static string? FeatureOf(string body)
    {
        try
        {
            using var problem = JsonDocument.Parse(body);
            return
                problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty("feature", out var feature)
                && feature.ValueKind == JsonValueKind.String
                ? feature.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
        catch (JsonException) { }
        return text.ToString();
    }
}
