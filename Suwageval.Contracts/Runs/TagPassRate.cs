namespace Suwageval.Contracts.Runs;

/// <summary>
/// The pass rate of a run over the checks of the scenarios that carry one tag, from 0 to 1, and the turn time of those scenarios'
/// samples (milliseconds; null when none of them made a call).
/// </summary>
public sealed record TagPassRate(string Tag, double PassRate, double? MedianMs = null, double? P95Ms = null);
