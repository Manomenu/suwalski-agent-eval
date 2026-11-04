using System.Globalization;

namespace Suwageval.Cli.Output;

internal static class TurnTime
{
    public static string Format(double? milliseconds) =>
        milliseconds is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value / 1000:0.0}s") : "-";
}
