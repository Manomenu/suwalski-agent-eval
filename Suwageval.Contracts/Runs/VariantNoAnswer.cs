namespace Suwageval.Contracts.Runs;

/// <summary>A sample where the agent gave no answer, with the error the run page shows for it.</summary>
public sealed record VariantNoAnswer
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    public string? Error { get; init; }
}
