namespace Suwageval.Contracts.Runs;

/// <summary>A run's pass rate over a set of checks (from 0 to 1; absent when the run has no such check) and its change against the baseline's (absent for the baseline itself and when either rate is absent).</summary>
public sealed record ComparedRate(double? PassRate = null, RateChange? Change = null);
