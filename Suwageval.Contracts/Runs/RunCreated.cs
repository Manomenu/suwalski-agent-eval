namespace Suwageval.Contracts.Runs;

public sealed record RunCreated
{
    public required Guid RunId { get; init; }

    /// <summary>The version the run is under test: the contract the agent served when the run was started.</summary>
    public required Guid AgentVersionId { get; init; }

    /// <summary><c>true</c> when that contract was new (a first version, or the agent changed); <c>false</c> when the same content was registered before.</summary>
    public required bool VersionCreated { get; init; }
}
