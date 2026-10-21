using Suwageval.Cli.Files;

namespace Suwageval.Cli.Server;

/// <summary>
/// Where Suwageval is: the web app's address, the one a browser opens. The API is under its <c>/api</c>, the way the browser reaches
/// it (nginx in the image, Vite's proxy on a laptop), so one address serves the commands and the links they print.
/// </summary>
internal static class ServerAddress
{
    public const string Variable = "SUWAGEVAL_URL";
    public const string Default = "http://localhost:3230";

    public static string OptionDescription =>
        $"Suwageval's address (the web app; the API is under /api). Default: ${Variable}, else suwagevalUrl in suwageval/agent.yaml, else {Default}.";

    /// <summary>
    /// From <c>--server</c>, else <see cref="Variable"/>, else the <c>suwagevalUrl</c> of the agent.yaml in <paramref name="agentDir"/> (when it is there),
    /// else <see cref="Default"/>; with a trailing slash, so relative paths stay under it.
    /// </summary>
    public static Uri Site(string? option, string agentDir = AgentFolder.DefaultDir) =>
        Site(option, Environment.GetEnvironmentVariable(Variable), () => AgentReader.SuwagevalUrl(agentDir));

    /// <summary>The precedence itself; <paramref name="fromFile"/> is only asked when the option and the variable say nothing.</summary>
    public static Uri Site(string? option, string? variable, Func<string?> fromFile)
    {
        ArgumentNullException.ThrowIfNull(fromFile);
        var address = string.IsNullOrWhiteSpace(option) ? variable : option;
        address = string.IsNullOrWhiteSpace(address) ? fromFile() : address;
        return new Uri((string.IsNullOrWhiteSpace(address) ? Default : address).TrimEnd('/') + "/");
    }

    public static Uri Api(Uri site) => new(site, "api/");

    /// <summary>The web page of a run.</summary>
    public static Uri RunPage(Uri site, Guid runId) => new(site, $"runs/{runId}");

    /// <summary>The web page comparing runs; the first is the baseline.</summary>
    public static Uri ComparePage(Uri site, IEnumerable<Guid> runIds) => new(site, $"compare?runs={string.Join(',', runIds)}");

    /// <summary>The variants page of runs named one by one, or of the runs of a group (of <paramref name="agent"/>, when known).</summary>
    public static Uri VariantsPage(Uri site, IReadOnlyCollection<Guid> runIds, string? group, string? agent) =>
        new(
            site,
            group is null
                ? $"variants?runs={string.Join(',', runIds)}"
                : $"variants?group={Uri.EscapeDataString(group)}{(agent is null ? "" : $"&agent={Uri.EscapeDataString(agent)}")}"
        );

    /// <summary>The web page showing what changed between two versions of an agent.</summary>
    public static Uri VersionComparePage(Uri site, string agent, Guid a, Guid b) =>
        new(site, $"agents/{Uri.EscapeDataString(agent)}/versions/compare?a={a}&b={b}");
}
