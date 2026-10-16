using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

/// <summary>
/// What a check's verdict in a run means against the check's history (the newest reference runs before it, in the same revisions of
/// the check and its scenario).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RegressionState>))]
public enum RegressionState
{
    /// <summary>Passes, and nothing more to say: it passed steadily before, or it has no history yet.</summary>
    Passed,

    /// <summary>Passed steadily before, fails now: this run broke it.</summary>
    Regression,

    /// <summary>Fails, and has no history yet (a new check, or one whose check or scenario changed): it was written to pass.</summary>
    New,

    /// <summary>Started failing in a reference run before this one, after passing steadily: broken, but not by this run.</summary>
    AlreadyBroken,

    /// <summary>Failed in every run of its history: known, nobody is surprised.</summary>
    KnownFailure,

    /// <summary>Failed before (known or already broken), passes now.</summary>
    Fixed,

    /// <summary>Its history is mixed: a verdict now says little either way.</summary>
    Unstable,
}
