namespace Suwageval.Contracts.Runs;

/// <summary>
/// For a tag's row, <see cref="ScenariosRan"/> of <see cref="ScenariosOfTag"/> says how much of the tag the run covered: no rate when not all of it.
/// A run's pass rate over a set of checks (from 0 to 1; absent when the run has no such check) and its change against the baseline's (absent for the baseline itself and when either rate is absent).</summary>
public sealed record ComparedRate(double? PassRate = null, RateChange? Change = null, int? ScenariosRan = null, int? ScenariosOfTag = null);
