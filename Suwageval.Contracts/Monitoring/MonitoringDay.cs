namespace Suwageval.Contracts.Monitoring;

/// <summary>
/// One day (UTC, <c>yyyy-MM-dd</c>, the day of the conversations' last turn) in one environment: how many conversations, how many of them
/// checked. A day has a row per environment that sent conversations on it.
/// </summary>
public sealed record MonitoringDay(string Date, string Environment, int Conversations, int Checked);
