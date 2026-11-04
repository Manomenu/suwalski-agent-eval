namespace Suwageval.Cli.Evaluation.Sweep;

internal sealed record VariantOutcome(string Variant, Guid? RunId, string Summary, bool Failed);
