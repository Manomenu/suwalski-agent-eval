namespace Suwageval.AgentSdk.Monitoring;

public sealed class MonitoringOptions
{
    public const string Section = "Suwageval";

    public string? Url { get; set; }

    public string? Agent { get; set; }

    public string? Environment { get; set; }

    public string? AccessClientId { get; set; }

    public string? AccessClientSecret { get; set; }
}
