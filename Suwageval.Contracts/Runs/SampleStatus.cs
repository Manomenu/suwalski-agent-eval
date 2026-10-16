using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>Whether the agent produced an answer for one sample of a scenario.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SampleStatus>))]
public enum SampleStatus
{
    Done,

    /// <summary>The agent could not be reached, timed out or answered something unreadable (or the scenario cannot run yet).</summary>
    Failed,
}
