namespace Suwageval.Contracts.Monitoring;

public sealed record MonitoringPoint(string Date, string CheckKey, Guid AgentVersionId, int Passed, int Checked);
