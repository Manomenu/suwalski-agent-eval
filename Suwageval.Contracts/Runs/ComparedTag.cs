namespace Suwageval.Contracts.Runs;

public sealed record ComparedTag
{
    public string? Tag { get; init; }

    public required IReadOnlyList<ComparedRate> Rates { get; init; }
}
