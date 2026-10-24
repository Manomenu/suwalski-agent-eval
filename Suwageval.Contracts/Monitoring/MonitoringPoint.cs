namespace Suwageval.Contracts.Monitoring;

/// <summary>One check on one day over the conversations one version answered: how many it was checked on, how many passed.</summary>
public sealed record MonitoringPoint(string Date, string CheckKey, Guid AgentVersionId, int Passed, int Checked);
