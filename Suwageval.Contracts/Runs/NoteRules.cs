namespace Suwageval.Contracts.Runs;

public static class NoteRules
{
    public const int MaxLength = 500;

    public const string Fix = "Shorten the note.";

    public static string? Normalize(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    public static string? Problem(string? note) =>
        Normalize(note) is { Length: > MaxLength } ? $"The note is at most {MaxLength} characters." : null;
}
