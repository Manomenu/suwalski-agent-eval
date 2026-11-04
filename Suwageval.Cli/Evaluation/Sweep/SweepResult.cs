namespace Suwageval.Cli.Evaluation.Sweep;

internal sealed record SweepResult(IReadOnlyList<VariantOutcome> Outcomes, bool Interrupted, bool RestoreFailed)
{
    public bool Failed => Interrupted || RestoreFailed || Outcomes.Any(outcome => outcome.Failed);
}
