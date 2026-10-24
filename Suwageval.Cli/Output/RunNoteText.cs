namespace Suwageval.Cli.Output;

internal static class RunNoteText
{
    /// <summary>The note on one line (its lines and runs of blanks joined by single spaces), after two spaces; nothing for a run without a note.</summary>
    public static string Suffix(string? note) => string.IsNullOrWhiteSpace(note) ? "" : $"  note {OneLine(note)}";

    /// <summary>The note on one line.</summary>
    public static string OneLine(string note) => string.Join(' ', note.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
