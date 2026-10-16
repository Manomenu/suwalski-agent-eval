using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

/// <summary>A check whose verdict a re-check would not copy from the run: it changed, was added or was removed since.</summary>
public sealed record ChangedCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    public required CheckChange Change { get; init; }

    /// <summary>The check as the run used it; null for an added check.</summary>
    public CheckInput? Before { get; init; }

    /// <summary>The check as it is now; null for a removed check.</summary>
    public CheckInput? After { get; init; }
}
