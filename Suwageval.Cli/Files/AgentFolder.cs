namespace Suwageval.Cli.Files;

/// <summary>
/// The files of an agent's <c>suwageval/</c> folder, read as text: <c>agent.yaml</c>, <c>scenarios/*.yaml</c> and
/// <c>monitoring-checks.yaml</c> (null: there is none).
/// </summary>
/// <remarks>AgentFile is the name of the agent file, for the problem when AgentText (null: the file does not exist) is missing.</remarks>
internal sealed record AgentFolder(
    string AgentFile,
    string? AgentText,
    IReadOnlyList<SourceText> Scenarios,
    SourceText? MonitoringChecks = null
)
{
    /// <summary>The file of the checks run on every monitored conversation of the agent.</summary>
    public const string MonitoringChecksFile = "monitoring-checks.yaml";

    /// <summary>Where a command looks for the agent's files when it is not told.</summary>
    public const string DefaultDir = "suwageval";

    public static AgentFolder Read(string dir)
    {
        var agentFile = Path.Combine(dir, "agent.yaml");
        var scenarios = Path.Combine(dir, "scenarios");
        var files = Directory.Exists(scenarios)
            ? Directory
                .EnumerateFiles(scenarios, "*.yaml")
                .Order(StringComparer.Ordinal)
                .Select(file => new SourceText(file, File.ReadAllText(file)))
            : [];
        var monitoringChecks = Path.Combine(dir, MonitoringChecksFile);
        return new AgentFolder(
            agentFile,
            File.Exists(agentFile) ? File.ReadAllText(agentFile) : null,
            [.. files],
            File.Exists(monitoringChecks) ? new SourceText(monitoringChecks, File.ReadAllText(monitoringChecks)) : null
        );
    }
}
