using System.Text.RegularExpressions;

namespace Suwageval.Cli.Files;

// Why: a console that is not UTF-8 turns typographic quotes into plain ones, breaking the JSON document; ASCII escapes survive any encoding
internal static partial class AsciiJson
{
    public static string Escape(string json) => NonAscii().Replace(json, character => $"\\u{(int)character.Value[0]:x4}");

    [GeneratedRegex("[^\x00-\x7F]")]
    private static partial Regex NonAscii();
}
