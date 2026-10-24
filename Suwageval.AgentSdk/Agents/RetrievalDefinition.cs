namespace Suwageval.AgentSdk.Agents;

/// <summary>The data an agent's retrieval reads, as the agent describes it.</summary>
public sealed record RetrievalDefinition
{
    /// <summary>
    /// An opaque fingerprint of what retrieval can return (the content and how it was indexed); required of an agent that uses
    /// retrieval (<see cref="AgentContractRules"/>). Outside the content hash: the server keeps it on the run, so new data is not a
    /// new agent version.
    /// </summary>
    public string? DataVersion { get; init; }

    /// <summary>For people (what the data is, how much of it). Outside the content hash, like the label.</summary>
    public string? Description { get; init; }
}
