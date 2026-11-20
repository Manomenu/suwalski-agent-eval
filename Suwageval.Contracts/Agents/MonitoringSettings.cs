namespace Suwageval.Contracts.Agents;

public sealed record MonitoringSettings
{
    public string? CloseAfter { get; init; }

    public IReadOnlyList<string>? Environments { get; init; }
}
