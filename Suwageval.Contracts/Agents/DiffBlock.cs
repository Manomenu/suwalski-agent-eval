namespace Suwageval.Contracts.Agents;

/// <summary>A stretch of a line diff: lines to show, or unchanged lines folded away until the reader opens them.</summary>
public sealed record DiffBlock
{
    public required bool Folded { get; init; }

    public required IReadOnlyList<DiffRow> Rows { get; init; }
}
