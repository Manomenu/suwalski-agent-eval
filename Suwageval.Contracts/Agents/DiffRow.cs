namespace Suwageval.Contracts.Agents;

public sealed record DiffRow
{
    public required DiffKind Kind { get; init; }

    public required string Text { get; init; }

    public int? FromLine { get; init; }

    public int? ToLine { get; init; }
}
