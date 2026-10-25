using System.Globalization;

namespace Suwageval.Cli.Output;

/// <summary>A share as the commands print it: 0.8 → "80%", rounded to a whole percent.</summary>
internal static class PercentText
{
    public static string Of(double share) => string.Create(CultureInfo.InvariantCulture, $"{share * 100:0}%");
}
