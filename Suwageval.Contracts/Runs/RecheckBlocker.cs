using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>Why a re-check cannot start.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecheckBlocker>))]
public enum RecheckBlocker
{
    /// <summary>The run has not finished (or did not finish): its answers are not all there.</summary>
    NotFinished,

    /// <summary>Every scenario of the run changed or was deleted since: its answers answer other conversations.</summary>
    AllSkipped,

    /// <summary>The current checks do not fit the version the run was under test.</summary>
    Mismatches,

    /// <summary>No check of the run's scenarios changed: the result would be the same.</summary>
    NothingChanged,
}
