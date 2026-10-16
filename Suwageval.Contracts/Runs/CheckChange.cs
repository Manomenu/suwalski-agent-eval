using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>What happened to a check since a run. A changed pass threshold is a change: it is part of the check's revision.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckChange>))]
public enum CheckChange
{
    Changed,
    Added,
    Removed,
}
