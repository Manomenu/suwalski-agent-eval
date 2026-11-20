namespace Suwageval.Contracts.Runs;

public sealed record RunCreated
{
    public required Guid RunId { get; init; }

    public required Guid AgentVersionId { get; init; }

    public required bool VersionCreated { get; init; }
}
