using System.Globalization;

namespace Suwageval.Cli.Output;

internal static class PercentText
{
    public static string Of(double share) => string.Create(CultureInfo.InvariantCulture, $"{share * 100:0}%");
}
