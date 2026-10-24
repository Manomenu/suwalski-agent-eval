namespace Suwageval.Contracts.Monitoring;

/// <summary>One day (UTC, <c>yyyy-MM-dd</c>, the day of the conversations' last turn): how many conversations, how many of them checked.</summary>
public sealed record MonitoringDay(string Date, int Conversations, int Checked);
