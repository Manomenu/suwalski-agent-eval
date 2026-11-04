namespace Suwageval.Cli.Files;

internal sealed record AgentFolder(
    string AgentFile,
    string? AgentText,
    IReadOnlyList<SourceText> Scenarios,
    SourceText? MonitoringChecks = null
)
{
    public const string MonitoringChecksFile = "monitoring-checks.yaml";

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
