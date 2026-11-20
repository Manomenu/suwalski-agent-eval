namespace Suwageval.Contracts.Monitoring;

public sealed record MonitoringDay(string Date, string Environment, int Conversations, int Checked);
