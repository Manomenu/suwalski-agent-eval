namespace Suwageval.Contracts.Runs;

/// <summary>Sets the note of a run or of a run group (<see cref="NoteRules"/>); none or blank clears it.</summary>
public sealed record NoteRequest
{
    public string? Note { get; init; }
}
