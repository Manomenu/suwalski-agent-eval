namespace Suwageval.Cli.Files;

/// <summary>One thing wrong in a file: where (file, path in the YAML, line and column when known) and what.</summary>
internal sealed record Problem(string File, string Path, string Message, long? Line, long? Column);
