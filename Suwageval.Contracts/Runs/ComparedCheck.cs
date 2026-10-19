using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

/// <summary>One check across the compared runs.</summary>
public sealed record ComparedCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    /// <summary>The check as the baseline ran it, or as the first run that has it ran it.</summary>
    public required CheckInput Definition { get; init; }

    public required bool JudgedByModel { get; init; }

    /// <summary>Some cell differs from the baseline's: another outcome or pass rate, or a check missing from a run.</summary>
    public required bool Differs { get; init; }

    /// <summary>Every run has this check, so the runs can be compared on it.</summary>
    public required bool Comparable { get; init; }

    /// <summary>One per compared run, in the order asked for; null for a run without the check (or without its scenario).</summary>
    public required IReadOnlyList<ComparedCell?> Cells { get; init; }
}
