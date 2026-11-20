namespace Suwageval.Contracts.Samples;

public static class SampleRules
{
    public static string? Problem(int? samples, string path) =>
        samples is < SampleSettings.Min or > SampleSettings.Max
            ? $"{path} is from {SampleSettings.Min} to {SampleSettings.Max} (the default is {SampleSettings.Default})."
            : null;
}
