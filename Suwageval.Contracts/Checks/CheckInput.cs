using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ToolCalledCheck), "tool-called")]
[JsonDerivedType(typeof(ToolNotCalledCheck), "tool-not-called")]
[JsonDerivedType(typeof(EventCheck), "event")]
[JsonDerivedType(typeof(EventNotEmittedCheck), "event-not-emitted")]
[JsonDerivedType(typeof(CitedCheck), "cited")]
[JsonDerivedType(typeof(TextCheck), "text")]
[JsonDerivedType(typeof(MessageCountCheck), "message-count")]
[JsonDerivedType(typeof(JudgeCheck), "judge")]
public abstract record CheckInput
{
    public required string Key { get; init; }

    public string ScenarioKey { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    public CheckWhere? Where { get; init; }

    public double PassThreshold { get; init; } = 1.0;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Samples { get; init; }
}
