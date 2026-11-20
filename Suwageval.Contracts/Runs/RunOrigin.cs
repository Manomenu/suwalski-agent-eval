namespace Suwageval.Contracts.Runs;

public sealed record RunOrigin
{
    public string? Branch { get; init; }

    public string? Commit { get; init; }

    public required bool Clean { get; init; }

    public required bool Pushed { get; init; }

    public string? DefaultBranch { get; init; }

    public string? TargetBranch { get; init; }

    public string? CompareWith { get; init; }
}
