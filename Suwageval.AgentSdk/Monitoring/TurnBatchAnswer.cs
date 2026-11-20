namespace Suwageval.AgentSdk.Monitoring;

public sealed record TurnBatchAnswer
{
    public required int Stored { get; init; }

    public bool SendContract { get; init; }
}
