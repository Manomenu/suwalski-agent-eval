using System.Globalization;

namespace Suwageval.Cli.Output;

/// <summary>A turn time as the text commands print it (<c>results</c>, <c>trend</c>).</summary>
internal static class TurnTime
{
    /// <summary>Seconds with one decimal; "-" when no sample made a call.</summary>
    public static string Format(double? milliseconds) =>
        milliseconds is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value / 1000:0.0}s") : "-";
}
