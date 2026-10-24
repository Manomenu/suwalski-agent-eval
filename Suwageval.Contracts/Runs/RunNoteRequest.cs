namespace Suwageval.Contracts.Runs;

/// <summary>Sets the note of a run (<see cref="RunNoteRules"/>); none or blank clears it.</summary>
public sealed record RunNoteRequest
{
    public string? Note { get; init; }
}
