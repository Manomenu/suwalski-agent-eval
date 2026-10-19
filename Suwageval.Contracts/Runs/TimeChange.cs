namespace Suwageval.Contracts.Runs;

/// <summary>
/// A turn time against the baseline's. For time lower is better, so <see cref="Trend.Better"/> is faster. A change under 5 % of the
/// baseline's time or under 100 ms counts as <see cref="Trend.Same"/> (a fast agent's noise).
/// </summary>
public sealed record TimeChange(double Milliseconds, Trend Trend);
