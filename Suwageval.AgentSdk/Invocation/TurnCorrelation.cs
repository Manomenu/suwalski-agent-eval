using Microsoft.AspNetCore.Http;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// Where a turn comes from, for the agent's traces and logs: suwageval sends it as headers, never in the body, and it never
/// changes what the agent answers. A member is null when its header is absent.
/// </summary>
public sealed record TurnCorrelation(string? RunId, string? Scenario, string? Sample)
{
    public const string RunIdHeader = "Suwageval-Run-Id";
    public const string ScenarioHeader = "Suwageval-Scenario";
    public const string SampleHeader = "Suwageval-Sample";

    internal static TurnCorrelation From(IHeaderDictionary headers) =>
        new(ValueOf(headers, RunIdHeader), ValueOf(headers, ScenarioHeader), ValueOf(headers, SampleHeader));

    private static string? ValueOf(IHeaderDictionary headers, string name) =>
        headers.TryGetValue(name, out var value) && value.ToString() is { Length: > 0 } text ? text : null;
}
