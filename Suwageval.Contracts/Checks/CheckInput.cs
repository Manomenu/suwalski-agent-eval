using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>
/// A test on what the agent produced, told apart by <c>type</c>: for a scenario (in a run), or for every monitored conversation of the
/// agent (a monitoring check, <c>suwageval/monitoring-checks.yaml</c>: no scenario, a description).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ToolCalledCheck), "tool-called")]
[JsonDerivedType(typeof(ToolNotCalledCheck), "tool-not-called")]
[JsonDerivedType(typeof(EventCheck), "event")]
[JsonDerivedType(typeof(EventNotEmittedCheck), "event-not-emitted")]
[JsonDerivedType(typeof(CitedCheck), "cited")]
[JsonDerivedType(typeof(TextCheck), "text")]
[JsonDerivedType(typeof(JudgeCheck), "judge")]
public abstract record CheckInput
{
    /// <summary>Lowercase kebab-case, unique within the agent.</summary>
    public required string Key { get; init; }

    /// <summary>The scenario the check belongs to; empty for a monitoring check, which belongs to the agent.</summary>
    public string ScenarioKey { get; init; } = "";

    /// <summary>What the check makes sure of, in plain words: required of a monitoring check, a note for a scenario's.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    public CheckWhere? Where { get; init; }

    /// <summary>The share of samples that must pass, from above 0 to 1.</summary>
    public double PassThreshold { get; init; } = 1.0;

    /// <summary>
    /// How many of its scenario's samples the check is judged on (the first ones, 1 to 20), and the number its pass rate and threshold
    /// count over. Not set: the scenario's samples.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }
}
