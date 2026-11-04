namespace Suwageval.Cli.Files;

internal sealed record SweepSettings
{
    public const int DefaultWaitSeconds = 180;

    public required string VariantsDir { get; init; }

    public required string Reconfigure { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public string? Restore { get; init; }

    public int WaitSeconds { get; init; } = DefaultWaitSeconds;

    public IEnumerable<(string Path, string Message)> Problems()
    {
        if (string.IsNullOrWhiteSpace(VariantsDir))
        {
            yield return ("variantsDir", "A folder is required.");
        }
        if (string.IsNullOrWhiteSpace(Reconfigure))
        {
            yield return ("reconfigure", "A command is required.");
        }
        foreach (var (name, command) in new[] { ("before", Before), ("after", After), ("restore", Restore) })
        {
            if (command is not null && string.IsNullOrWhiteSpace(command))
            {
                yield return (name, "A command, or leave it out.");
            }
        }
        if (WaitSeconds is < 1 or > 3600)
        {
            yield return ("waitSeconds", "Between 1 and 3600.");
        }
    }
}
