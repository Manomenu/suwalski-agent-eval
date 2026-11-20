namespace Suwageval.Contracts.Runs;

public sealed record TagPassRate(string Tag, double PassRate, double? MedianMs = null, double? P95Ms = null);
