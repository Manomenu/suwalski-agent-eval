namespace Suwageval.Cli.Files;

/// <summary>A file's name (as shown in problems) and its content.</summary>
internal sealed record SourceText(string File, string Text);
