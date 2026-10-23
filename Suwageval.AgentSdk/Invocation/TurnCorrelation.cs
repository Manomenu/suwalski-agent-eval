using Microsoft.AspNetCore.Http;

namespace Suwageval.AgentSdk.Invocation;

/// <summary>
/// Where a turn comes from, for the agent's traces and logs: suwageval sends it in the W3C <c>baggage</c> header
/// (<see cref="SuwagevalBaggage"/>), never in the body, and it never changes what the agent answers. A member is null (a list:
/// empty) when its key is absent.
/// </summary>
/// <param name="RunId">The run's id (a GUID).</param>
/// <param name="Scenario">The scenario's key.</param>
/// <param name="ScenarioType">Whether the scenario is a recorded turn or a simulation.</param>
/// <param name="Sample">The sample's number, from 1.</param>
/// <param name="ScenarioTags">The scenario's tags from its YAML.</param>
/// <param name="RunTags">The tags the run was limited to; empty for a run of every scenario.</param>
/// <param name="RunGroup">The run's group; null outside a group.</param>
/// <param name="AgentVersionId">The id Suwageval registered the agent's version under (its version page); the agent's own label and
/// release are its own to report.</param>
public sealed record TurnCorrelation(
    string? RunId,
    string? Scenario,
    ScenarioType? ScenarioType,
    string? Sample,
    IReadOnlyList<string> ScenarioTags,
    IReadOnlyList<string> RunTags,
    string? RunGroup,
    string? AgentVersionId = null
)
{
    /// <summary>A turn with no baggage.</summary>
    public static TurnCorrelation None { get; } = new(null, null, null, null, [], [], null);

    internal static TurnCorrelation From(IHeaderDictionary headers)
    {
        var members = SuwagevalBaggage.Decode(
            headers.TryGetValue(SuwagevalBaggage.HeaderName, out var values) ? values.OfType<string>() : []
        );
        return new TurnCorrelation(
            ValueOf(members, SuwagevalBaggage.RunId),
            ValueOf(members, SuwagevalBaggage.ScenarioKey),
            Enum.TryParse<ScenarioType>(ValueOf(members, SuwagevalBaggage.ScenarioTypeKey), out var type) ? type : null,
            ValueOf(members, SuwagevalBaggage.ScenarioSample),
            ListOf(members, SuwagevalBaggage.ScenarioTags),
            ListOf(members, SuwagevalBaggage.RunTags),
            ValueOf(members, SuwagevalBaggage.RunGroup),
            ValueOf(members, SuwagevalBaggage.AgentVersionId)
        );
    }

    private static string? ValueOf(IReadOnlyDictionary<string, string> members, string key) =>
        members.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    private static string[] ListOf(IReadOnlyDictionary<string, string> members, string key) =>
        ValueOf(members, key)?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
}
