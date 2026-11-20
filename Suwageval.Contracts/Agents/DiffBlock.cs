namespace Suwageval.Contracts.Agents;

public sealed record DiffBlock
{
    public required bool Folded { get; init; }

    public required IReadOnlyList<DiffRow> Rows { get; init; }
}
