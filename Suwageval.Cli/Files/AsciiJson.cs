using System.Text.RegularExpressions;

namespace Suwageval.Cli.Files;

internal static partial class AsciiJson
{
    public static string Escape(string json) => NonAscii().Replace(json, character => $"\\u{(int)character.Value[0]:x4}");

    [GeneratedRegex("[^\x00-\x7F]")]
    private static partial Regex NonAscii();
}
