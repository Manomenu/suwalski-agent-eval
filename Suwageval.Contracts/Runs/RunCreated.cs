namespace Suwageval.Contracts.Runs;

public sealed record RunCreated
{
    public required Guid RunId { get; init; }
}
