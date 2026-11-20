namespace Suwageval.Contracts.Runs;

public sealed record VariantNoAnswer
{
    public required int Sample { get; init; }

    public string? Error { get; init; }
}
