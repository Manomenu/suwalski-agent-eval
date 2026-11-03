using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Scenarios;

/// <summary>
/// The acceptance criterion a scenario guards (<c>acGuardrail:</c>): <c>suwageval gate</c> runs every scenario that has one, and the
/// scenario passes on its first attempt in which every check passes, out of at most <see cref="RetryLimit"/> attempts.
/// </summary>
public sealed record AcGuardrail
{
    /// <summary>The criterion's id, <c>AC-&lt;feature&gt;-&lt;n&gt;</c> (<see cref="AcGuardrailRules"/>), unique within the agent.</summary>
    public required string Id { get; init; }

    /// <summary>The most attempts the gate makes (1 to 20); not set: the agent's <c>acGuardrailRetryLimit</c>, else 5.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryLimit { get; init; }
}
