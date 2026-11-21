namespace Suwageval.Contracts.Agents;

public sealed record VersionDiff
{
    public required AgentVersion From { get; init; }

    public required AgentVersion To { get; init; }

    public required int Added { get; init; }

    public required int Removed { get; init; }

    public required IReadOnlyList<DiffBlock> Blocks { get; init; }

    public required string FromYaml { get; init; }

    public required string ToYaml { get; init; }
}
