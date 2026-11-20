namespace Suwageval.Contracts.Runs;

public sealed record VariantFailure
{
    public required int Sample { get; init; }

    public required string Message { get; init; }
}
