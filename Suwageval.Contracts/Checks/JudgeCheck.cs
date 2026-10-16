namespace Suwageval.Contracts.Checks;

/// <summary>A model's verdict on what the agent produced, against criteria written in plain words.</summary>
public sealed record JudgeCheck : CheckInput
{
    /// <summary>What the agent's output must satisfy, in one or a few sentences; the judge answers whether it does.</summary>
    public required string Criteria { get; init; }
}
