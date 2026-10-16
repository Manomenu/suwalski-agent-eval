using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>A test on what the agent produced for a scenario, told apart by <c>type</c>.</summary>
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

    public required string ScenarioKey { get; init; }

    public CheckWhere? Where { get; init; }

    /// <summary>The share of samples that must pass, from above 0 to 1.</summary>
    public double PassThreshold { get; init; } = 1.0;
}
