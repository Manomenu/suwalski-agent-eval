namespace Suwageval.Cli.Files;

/// <summary>
/// The <c>sweep:</c> section of <c>agent.yaml</c>, read by <c>suwageval sweep</c> only. In every command line <c>{variant}</c> and
/// <c>{group}</c> are replaced by the variant being run and the run group of the sweep.
/// </summary>
internal sealed record SweepSettings
{
    public const int DefaultWaitSeconds = 180;

    /// <summary>Relative to the folder of agent.yaml. Every file in it is a variant, named like the file without its extension.</summary>
    public required string VariantsDir { get; init; }

    /// <summary>Leaves the agent serving the variant: its contract's <c>label</c> must then be the variant's name.</summary>
    public required string Reconfigure { get; init; }

    /// <summary>Runs before <see cref="Reconfigure"/> (starting a model), for each variant.</summary>
    public string? Before { get; init; }

    /// <summary>Runs after each variant, whatever happened to it, and when the sweep is interrupted.</summary>
    public string? After { get; init; }

    /// <summary>Runs once when the sweep ends, whatever happened: puts the agent back to its default.</summary>
    public string? Restore { get; init; }

    /// <summary>How long the agent's contract may take to show the variant's label after <see cref="Reconfigure"/>.</summary>
    public int WaitSeconds { get; init; } = DefaultWaitSeconds;

    /// <summary>What is wrong with the values, as (path in the section, message).</summary>
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
