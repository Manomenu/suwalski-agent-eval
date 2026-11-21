using Suwageval.Cli.Files;
using Suwbackkit.Cli;

namespace Suwageval.Cli.Server;

internal static class ServerAddress
{
    public const string Variable = "SUWAGEVAL_URL";
    public const string Default = "http://localhost:3230";

    public static string OptionDescription =>
        $"Suwageval's address (the web app; the API is under /api). Default: ${Variable}, else suwagevalUrl in suwageval/agent.yaml, else {Default}.";

    public static AddressChoice Site(string? option, string agentDir = AgentFolder.DefaultDir) =>
        Site(option, Environment.GetEnvironmentVariable(Variable), () => AgentReader.SuwagevalUrl(agentDir));

    public static AddressChoice Site(string? option, string? variable, Func<string?> fromFile) =>
        Suwbackkit.Cli.ServerAddress.Choose(
            [new("--server", () => option), new(Variable, () => variable), new("suwagevalUrl in agent.yaml", fromFile)],
            Default
        );

    public static Uri Api(Uri site) => Suwbackkit.Cli.ServerAddress.Api(site);

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

    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value ?? "").Replace("%2C", ",", StringComparison.Ordinal)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    public static Uri VersionComparePage(Uri site, string agent, Guid a, Guid b) =>
        new(site, $"agents/{Uri.EscapeDataString(agent)}/versions/compare?a={a}&b={b}");
}
