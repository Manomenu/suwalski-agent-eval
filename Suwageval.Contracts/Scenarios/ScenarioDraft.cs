namespace Suwageval.Contracts.Scenarios;

/// <summary>
/// A scenario file drafted from a monitored conversation, for a person to review and put in the agent's repository: its proposed
/// <see cref="Key"/>, the file's <see cref="Path"/> in the repository and its <see cref="Yaml"/> (with the monitoring checks the conversation failed as comments).
/// </summary>
public sealed record ScenarioDraft
{
    public required string Key { get; init; }

    /// <summary>Where the file goes, relative to the repository's root: <c>suwageval/scenarios/&lt;key&gt;.yaml</c>.</summary>
    public required string Path { get; init; }

    public required string Yaml { get; init; }
}
