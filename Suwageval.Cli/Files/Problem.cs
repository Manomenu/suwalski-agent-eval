namespace Suwageval.Cli.Files;

internal sealed record Problem(string File, string Path, string Message, long? Line, long? Column);
