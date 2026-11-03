namespace Suwageval.Contracts.Runs;

/// <summary>
/// What a gate run (<c>suwageval gate</c>) came to: passed when every acceptance criterion it ran was met, failed at the first one
/// that used up its attempts. Read it only once the run is done; a run that did not finish answers nothing.
/// </summary>
public sealed record GateResult
{
    public required bool Passed { get; init; }

    /// <summary>The acceptance criterion that used up its attempts; none when the gate passed.</summary>
    public string? FailedGuardrail { get; init; }
}
