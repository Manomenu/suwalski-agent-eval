namespace Suwageval.Contracts.Checks;

/// <summary>On the agent's answer text; at least one of the three is given, all given must hold.</summary>
public sealed record TextCheck : CheckInput
{
    public string? Contains { get; init; }

    public string? NotContains { get; init; }

    /// <summary>A regular expression the text must match.</summary>
    public string? Pattern { get; init; }
}
