using Suwageval.Cli.Files;

namespace Suwageval.Cli.Server;

internal static class ServerAddress
{
    public const string Variable = "SUWAGEVAL_URL";
    public const string Default = "http://localhost:3230";

    public static string OptionDescription =>
        $"Suwageval's address (the web app; the API is under /api). Default: ${Variable}, else suwagevalUrl in suwageval/agent.yaml, else {Default}.";

    public static Uri Site(string? option, string agentDir = AgentFolder.DefaultDir) =>
        Site(option, Environment.GetEnvironmentVariable(Variable), () => AgentReader.SuwagevalUrl(agentDir));

    public static Uri Site(string? option, string? variable, Func<string?> fromFile)
    {
        ArgumentNullException.ThrowIfNull(fromFile);
        var address = string.IsNullOrWhiteSpace(option) ? variable : option;
        address = string.IsNullOrWhiteSpace(address) ? fromFile() : address;
        return new Uri((string.IsNullOrWhiteSpace(address) ? Default : address).TrimEnd('/') + "/");
    }

    public static Uri Api(Uri site) => new(site, "api/");

    public static Uri RunPage(Uri site, Guid runId, bool print = false) => new(site, $"runs/{runId}{(print ? "?print=1" : "")}");

    public static Uri ComparePage(
        Uri site,
        IReadOnlyCollection<Guid> runIds,
        string? group,
        string? agent,
        Guid? baseline,
        bool print = false
    ) =>
        new(
            site,
            (
                group is null
                    ? $"compare?runs={string.Join(',', runIds)}"
                    : $"compare?group={Uri.EscapeDataString(group)}{(agent is null ? "" : $"&agent={Uri.EscapeDataString(agent)}")}"
            )
                + (baseline is null ? "" : $"&baseline={baseline}")
                + (print ? "&print=1" : "")
        );

    public static Uri AgentsPage(Uri site) => new(site, "");

    public static Uri VersionsPage(
        Uri site,
        string agent,
        IReadOnlyCollection<string> usages,
        IReadOnlyCollection<Guid> versions,
        string? environment
    ) =>
        new(
            site,
            $"agents/{Uri.EscapeDataString(agent)}"
                + Query(
                    ("usage", usages.Count == 0 ? null : string.Join(',', usages)),
                    ("version", versions.Count == 0 ? null : string.Join(',', versions)),
                    ("environment", environment)
                )
        );

    public static Uri VersionPage(Uri site, string agent, Guid versionId) =>
        new(site, $"agents/{Uri.EscapeDataString(agent)}/versions/{versionId}");

    public static Uri EvaluationPage(Uri site, string agent) => new(site, $"agents/{Uri.EscapeDataString(agent)}/evaluation");

    public static Uri MonitoringPage(Uri site, string agent, string? environment = null, string? failedCheck = null) =>
        new(
            site,
            $"agents/{Uri.EscapeDataString(agent)}/monitoring"
                + Query(("failedCheck", failedCheck), ("environment", environment))
                + (failedCheck is null ? "" : "#conversations")
        );

    public static Uri ConversationPage(Uri site, Guid id) => new(site, $"conversations/{id}");

    // Why: commas stay unescaped to match the web's own addresses
    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            // Why: parts were filtered by IsNullOrWhiteSpace above, so Value is not null
            .Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!).Replace("%2C", ",", StringComparison.Ordinal)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    public static Uri VersionComparePage(Uri site, string agent, Guid a, Guid b) =>
        new(site, $"agents/{Uri.EscapeDataString(agent)}/versions/compare?a={a}&b={b}");
}
