namespace Suwageval.Cli.Server;

internal sealed record Refusal(string? Code, string? Title, string? Detail, IReadOnlyList<RefusalMistake>? Mistakes);
