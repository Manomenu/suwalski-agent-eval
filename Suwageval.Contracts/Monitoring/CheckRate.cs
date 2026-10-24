namespace Suwageval.Contracts.Monitoring;

/// <summary>How a monitoring check did over some conversations: how many it was checked on, how many passed.</summary>
public sealed record CheckRate(int Passed, int Checked);
