namespace Suwageval.Contracts.Runs;

/// <summary>A pass rate against the baseline's: the difference in percentage points rounded to one decimal, and whether that is better, worse or the same.</summary>
public sealed record RateChange(double Points, Trend Trend);
