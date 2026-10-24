namespace Suwageval.AgentSdk.Monitoring;

/// <summary>
/// Where a monitored agent sends its conversations: the section <c>Suwageval</c> of its configuration (<c>Suwageval__Url</c> and so on
/// in the environment). Without <see cref="Url"/> nothing is sent.
/// </summary>
public sealed class MonitoringOptions
{
    public const string Section = "Suwageval";

    /// <summary>Suwageval's address, the one its web app answers at (the API is under <c>/api</c>).</summary>
    public string? Url { get; set; }

    /// <summary>The agent's name in Suwageval: <c>name</c> in its <c>agent.yaml</c>.</summary>
    public string? Agent { get; set; }

    /// <summary>Where this deployment runs (<c>production</c>, <c>staging</c>…): one the agent's <c>features.monitoring.environments</c> takes.</summary>
    public string? Environment { get; set; }

    /// <summary>A Cloudflare Access service token, when Suwageval sits behind Access: its client id and secret (a secret of the deployment).</summary>
    public string? AccessClientId { get; set; }

    public string? AccessClientSecret { get; set; }
}
