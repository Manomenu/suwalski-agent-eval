namespace Suwageval.Contracts.Agents;

/// <summary>
/// What changed between two versions of an agent: both contracts written as YAML and compared line by line. <see cref="From"/> is the
/// older version (the base), <see cref="To"/> the newer; versions created at the same time stay in the order asked for.
/// </summary>
public sealed record VersionDiff
{
    public required AgentVersion From { get; init; }

    public required AgentVersion To { get; init; }

    /// <summary>How many lines <see cref="To"/> adds and removes compared with <see cref="From"/>.</summary>
    public required int Added { get; init; }

    public required int Removed { get; init; }

    /// <summary>The hunks: every change with three unchanged lines around it, a longer unchanged run folded into one block; none for the same content.</summary>
    public required IReadOnlyList<DiffBlock> Blocks { get; init; }
}
