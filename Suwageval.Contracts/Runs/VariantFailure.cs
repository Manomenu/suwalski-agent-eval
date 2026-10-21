namespace Suwageval.Contracts.Runs;

/// <summary>Why one sample did not pass a check: the check's message for it.</summary>
public sealed record VariantFailure
{
    /// <summary>From 1.</summary>
    public required int Sample { get; init; }

    public required string Message { get; init; }
}
