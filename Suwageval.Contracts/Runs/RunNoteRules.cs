namespace Suwageval.Contracts.Runs;

/// <summary>What a run's note may be: free text, trimmed, at most <see cref="MaxLength"/> characters; blank means none.</summary>
public static class RunNoteRules
{
    public const int MaxLength = 500;

    /// <summary>The note as it is stored: trimmed, and none when blank.</summary>
    public static string? Normalize(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>What is wrong with <paramref name="note"/>, or null when it is fine (or none).</summary>
    public static string? Problem(string? note) =>
        Normalize(note) is { Length: > MaxLength } ? $"The note is at most {MaxLength} characters." : null;
}
