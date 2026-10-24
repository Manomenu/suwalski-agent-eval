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

    /// <summary>A new run with the agent, tags and endpoint of run <paramref name="runId"/> (the endpoint unless another is given), in <paramref name="group"/> if any, with <paramref name="note"/> if any.</summary>
    public Task<RunCreated> RerunAsync(Guid runId, string? endpointUrl, string? group, string? note, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () =>
                http.PostAsJsonAsync(
                    new Uri($"eval/runs/{runId}/rerun", UriKind.Relative),
                    new RerunRequest
                    {
                        EndpointUrl = endpointUrl,
                        Group = group,
                        Note = note,
                    },
                    ContractJson.Options,
                    cancellationToken
                ),
            cancellationToken
        );

    /// <summary>What a re-check of run <paramref name="runId"/> would do and whether it can start.</summary>
    public Task<RecheckPlan> RecheckPlanAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RecheckPlan>(
            () => http.GetAsync(new Uri($"eval/runs/{runId}/recheck", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>Starts a re-check of run <paramref name="runId"/>: its stored answers judged again by the current checks.</summary>
    public Task<RunCreated> RecheckAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync<RunCreated>(
            () => http.PostAsync(new Uri($"eval/runs/{runId}/recheck", UriKind.Relative), null, cancellationToken),
            cancellationToken
        );

    /// <summary>Asks the server to stop run <paramref name="runId"/>: it answers 202 for any known run, a finished one stays as it is.</summary>
    public Task CancelAsync(Guid runId, CancellationToken cancellationToken) =>
        SendAsync(() => http.PostAsync(new Uri($"eval/runs/{runId}/cancel", UriKind.Relative), null, cancellationToken), cancellationToken);

    /// <summary>Sets the note of run <paramref name="runId"/>; none or blank clears it.</summary>
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

    /// <summary>Sets the note of the agent's run group <paramref name="group"/>; none or blank clears it. 404: no run of the agent is in the group.</summary>
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

    /// <summary>
    /// The agent's newest runs, newest first, at most <paramref name="limit"/> of them (asked for in pages the server accepts); only those of
    /// <paramref name="versions"/> when any are given, and, with <paramref name="referenceOnly"/>, only reference runs (the server has no
    /// filter for those, so older pages are read until enough are found); only those started in <paramref name="group"/> when one is given;
    /// only those in no group or in some group as <paramref name="grouping"/> says;
    /// only those that ran the scenarios of any of <paramref name="tags"/> (limited to tags including one of them, or not limited) when some are given;
    /// only those on one of <paramref name="dataVersions"/> when some are given.
    /// </summary>
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
        // One page size throughout: the server pages by number, so changing it between requests would skip or repeat runs.
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

    /// <summary>The groups of the agent's runs, the one with the newest run first.</summary>
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

    /// <summary>What deleting the run group would remove; the server answers like the deletion would (409 for a group that is not archived or has a run in progress).</summary>
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

    /// <summary>Deletes an archived run group with its runs, samples and verdicts.</summary>
    public Task DeleteRunGroupAsync(string agent, string group, CancellationToken cancellationToken) =>
        SendAsync(
            () =>
                http.DeleteAsync(
                    new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/run-groups/{Uri.EscapeDataString(group)}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    /// <summary>One agent with what is stored for it; the server answers 404 for an agent it does not have.</summary>
    public Task<AgentDetail> AgentAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<AgentDetail>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>
    /// The agent's newest monitored conversations (the newest last turn first), at most <paramref name="limit"/>, only those of
    /// <paramref name="environment"/>, of <paramref name="versions"/> and in <paramref name="state"/> when given.
    /// </summary>
    public async Task<ConversationList> ConversationsAsync(
        string agent,
        int limit,
        string? environment,
        IReadOnlyList<Guid> versions,
        ConversationState? state,
        CancellationToken cancellationToken
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
        return await SendAsync<ConversationList>(() => http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken), cancellationToken);
    }

    /// <summary>A monitored conversation drafted as a scenario file (its key, path and YAML).</summary>
    public Task<ScenarioDraft> ScenarioDraftAsync(Guid conversationId, CancellationToken cancellationToken) =>
        SendAsync<ScenarioDraft>(
            () => http.GetAsync(new Uri($"monitoring/conversations/{conversationId}/scenario", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>A monitored conversation with its turns.</summary>
    public Task<ConversationDetail> ConversationAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<ConversationDetail>(
            () => http.GetAsync(new Uri($"monitoring/conversations/{id}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    public Task<IReadOnlyList<Agent>> AgentsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<Agent>>(() => http.GetAsync(new Uri("agents", UriKind.Relative), cancellationToken), cancellationToken);

    /// <summary>The agent's scenarios (the newest revision of each), only those with <paramref name="tag"/> when one is given.</summary>
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

    /// <summary>The agent's checks (the newest revision of each), only those of scenario <paramref name="scenario"/> when one is given.</summary>
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

    /// <summary>The points of the agent's trend: one per finished run, with its pass rate, turn times and rate per tag.</summary>
    public Task<IReadOnlyList<RunPassRates>> PassRatesAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<RunPassRates>>(
            () => http.GetAsync(new Uri($"eval/agents/{Uri.EscapeDataString(agent)}/pass-rates", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>
    /// The agent's open unstable checks, most often failing first: unstable in the newest version that ran them at least twice, among
    /// <paramref name="versions"/> (none: every version not archived), over the runs on <paramref name="dataVersions"/> (none: any data).
    /// </summary>
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

    /// <summary>
    /// The agent's open errors, the highest share of samples first: scenarios where the agent gave no answer in the newest version that ran
    /// them, among <paramref name="versions"/> (none: every version not archived), over the runs on <paramref name="dataVersions"/> (none: any data).
    /// </summary>
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

    /// <summary>Asks for the run until it is done, failed or cancelled.</summary>
    public async Task WaitForEndAsync(Guid runId, CancellationToken cancellationToken)
    {
        while ((await GetAsync(runId, cancellationToken)).Status is RunStatus.Queued or RunStatus.Running)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// The runs named by <paramref name="runIds"/>, or those of <paramref name="group"/> (of <paramref name="agent"/> when given), side by
    /// side; with <paramref name="baseline"/> (one of the runs) also how the others stand against it.
    /// </summary>
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

    /// <summary>What changed between two versions of the agent, line by line.</summary>
    public Task<VersionDiff> VersionDiffAsync(string agent, Guid from, Guid to, CancellationToken cancellationToken) =>
        SendAsync<VersionDiff>(
            () =>
                http.GetAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/diff?from={from}&to={to}", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    /// <summary>The agent's versions, newest first; <paramref name="grouping"/> says whether to leave out (<c>None</c>) or keep only (<c>Any</c>) those whose every run is in a group.</summary>
    public Task<IReadOnlyList<AgentVersion>> VersionsAsync(
        string agent,
        CancellationToken cancellationToken,
        RunGrouping grouping = RunGrouping.All
    ) =>
        SendAsync<IReadOnlyList<AgentVersion>>(
            () =>
                http.GetAsync(
                    new Uri(
                        $"agents/{Uri.EscapeDataString(agent)}/versions{(grouping == RunGrouping.All ? "" : $"?grouping={grouping}")}",
                        UriKind.Relative
                    ),
                    cancellationToken
                ),
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

    /// <summary>What deleting the agent would remove; the server answers like the deletion would (409 for an agent with a run in progress).</summary>
    public Task<AgentDeletion> AgentDeletionAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync<AgentDeletion>(
            () => http.GetAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}/deletion", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>Deletes the agent with its versions, scenarios, checks, runs, samples and verdicts.</summary>
    public Task DeleteAgentAsync(string agent, CancellationToken cancellationToken) =>
        SendAsync(
            () => http.DeleteAsync(new Uri($"agents/{Uri.EscapeDataString(agent)}", UriKind.Relative), cancellationToken),
            cancellationToken
        );

    /// <summary>What deleting the version would remove; the server answers like the deletion would (409 for a version that is not archived or has a run in progress).</summary>
    public Task<VersionDeletion> VersionDeletionAsync(string agent, Guid versionId, CancellationToken cancellationToken) =>
        SendAsync<VersionDeletion>(
            () =>
                http.GetAsync(
                    new Uri($"agents/{Uri.EscapeDataString(agent)}/versions/{versionId}/deletion", UriKind.Relative),
                    cancellationToken
                ),
            cancellationToken
        );

    /// <summary>Deletes an archived version with its runs, samples and verdicts.</summary>
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

    /// <summary>For an answer with no body.</summary>
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

    /// <summary>The area a refusal says is switched off (its <c>feature</c> extension), or null.</summary>
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
