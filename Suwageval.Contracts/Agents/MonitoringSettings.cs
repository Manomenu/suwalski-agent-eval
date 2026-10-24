namespace Suwageval.Contracts.Agents;

/// <summary>How the conversations of a monitored agent are taken (<c>features.monitoring</c> in <c>agent.yaml</c>), every part optional.</summary>
public sealed record MonitoringSettings
{
    /// <summary>
    /// How long a conversation may go without a turn before it is closed and its checks run: a number with <c>s</c>, <c>m</c> or <c>h</c>
    /// (<c>30m</c>, the default; from one minute to a day). A turn after that opens it again.
    /// </summary>
    public string? CloseAfter { get; init; }

    /// <summary>
    /// The environments whose conversations are taken (the running agent names its own, <c>Suwageval__Environment</c>); none: any.
    /// Lowercase words joined by hyphens (<c>production</c>, <c>staging</c>, <c>demo-eu</c>).
    /// </summary>
    public IReadOnlyList<string>? Environments { get; init; }
}
