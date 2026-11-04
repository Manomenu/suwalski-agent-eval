namespace Suwageval.Cli.Output;

internal static class RunNoteText
{
    public static string Suffix(string? note) => string.IsNullOrWhiteSpace(note) ? "" : $"  note {OneLine(note)}";

    public static string OneLine(string note) => string.Join(' ', note.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
