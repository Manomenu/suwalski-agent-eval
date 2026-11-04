namespace Suwageval.Cli.Evaluation.Sweep;

/// <param name="Outcomes">One per variant that was attempted, in order.</param>
/// <param name="Interrupted">Ctrl+C stopped the sweep: the variants after the interrupted one were not run.</param>
/// <param name="RestoreFailed">The <c>restore</c> command failed: the agent may still serve the last variant.</param>
internal sealed record SweepResult(IReadOnlyList<VariantOutcome> Outcomes, bool Interrupted, bool RestoreFailed)
{
    public bool Failed => Interrupted || RestoreFailed || Outcomes.Any(outcome => outcome.Failed);
}
