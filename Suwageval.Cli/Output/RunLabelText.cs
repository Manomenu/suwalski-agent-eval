namespace Suwageval.Cli.Output;

internal static class RunLabelText
{
    public static string Suffix(string? label) => string.IsNullOrWhiteSpace(label) ? "" : $"  label {label}";
}
