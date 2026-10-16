using Suwageval.Cli.Validate;

namespace Suwageval.Cli.Files;

/// <summary>The files of an agent's <c>suwageval/</c> folder, read as text: <c>agent.yaml</c> and <c>scenarios/*.yaml</c>.</summary>
/// <remarks>AgentFile is the name of the agent file, for the problem when AgentText (null: the file does not exist) is missing.</remarks>
internal sealed record AgentFolder(string AgentFile, string? AgentText, IReadOnlyList<SourceText> Scenarios)
{
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
        return new AgentFolder(agentFile, File.Exists(agentFile) ? File.ReadAllText(agentFile) : null, [.. files]);
    }

    public ValidationResult Validate() => Validator.Validate(AgentFile, AgentText, Scenarios);
}
