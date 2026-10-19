namespace Suwageval.Contracts.Agents;

/// <summary>One line of a line diff with its number on each side.</summary>
public sealed record DiffRow
{
    public required DiffKind Kind { get; init; }

    public required string Text { get; init; }

    /// <summary>The line's number (from 1) in the older text; absent for an added line.</summary>
    public int? FromLine { get; init; }

    /// <summary>The line's number (from 1) in the newer text; absent for a removed line.</summary>
    public int? ToLine { get; init; }
}
