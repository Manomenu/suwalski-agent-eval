namespace Suwageval.Cli.Evaluation.Sweep;

/// <summary>What happened to one variant: its run (if one was started) and one line about it.</summary>
internal sealed record VariantOutcome(string Variant, Guid? RunId, string Summary, bool Failed);
