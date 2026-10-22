using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>Whether the agent produced an answer for one sample of a scenario.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SampleStatus>))]
public enum SampleStatus
{
    Done,

    /// <summary>The agent could not be reached, timed out or answered something unreadable (or the scenario cannot run yet).</summary>
    Failed,

    /// <summary>
    /// A simulation stopped because the simulated user could not be played: the simulator's model was unreachable, too slow or answered
    /// something unreadable. Not the agent's doing, so it never counts against the agent.
    /// </summary>
    NotSimulated,
}
