namespace Suwageval.Contracts.Samples;

/// <summary>Whether a <c>samples</c> value is valid, written once for every level and for everyone who validates it (the CLI and the server).</summary>
public static class SampleRules
{
    /// <summary>The problem with <paramref name="samples"/> (not set is fine); <paramref name="path"/> is where it sits (<c>checks[0].samples</c>).</summary>
    public static string? Problem(int? samples, string path) =>
        samples is < SampleSettings.Min or > SampleSettings.Max
            ? $"{path} is from {SampleSettings.Min} to {SampleSettings.Max} (the default is {SampleSettings.Default})."
            : null;
}
