namespace Suwageval.AgentSdk.Monitoring;

/// <summary>The server's answer to a <see cref="TurnBatch"/>.</summary>
public sealed record TurnBatchAnswer
{
    /// <summary>How many of the turns were new (a turn sent before is not stored twice).</summary>
    public required int Stored { get; init; }

    /// <summary>The server does not know the contract hash: nothing was stored, send the same turns again with the contract.</summary>
    public bool SendContract { get; init; }
}
