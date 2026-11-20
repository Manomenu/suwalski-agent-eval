using Microsoft.AspNetCore.Http;

namespace Suwageval.AgentSdk.Invocation;

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
