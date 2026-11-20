namespace Suwageval.Contracts.Runs;

public sealed record GateResult
{
    public required bool Passed { get; init; }

    public string? FailedGuardrail { get; init; }
}
