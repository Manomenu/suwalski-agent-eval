namespace Suwageval.Contracts.Runs;

/// <summary>The pass rate of a run over the checks of the scenarios that carry one tag, from 0 to 1.</summary>
public sealed record TagPassRate(string Tag, double PassRate);
