using System.Text.RegularExpressions;

namespace Suwageval.Cli.Files;

/// <summary>
/// What every <c>--json</c> output goes through. A console that is not UTF-8 (a Windows code page) turns a typographic quote in a
/// reply into a plain <c>"</c> or <c>?</c> on its way out, which breaks the document; JSON written in ASCII survives any encoding.
/// </summary>
internal static partial class AsciiJson
{
    /// <summary>The JSON with every non-ASCII character as a <c>\uXXXX</c> escape: outside a string JSON has none, so only strings change.</summary>
    public static string Escape(string json) =>
        NonAscii().Replace(json, character => $"\\u{(int)character.Value[0]:x4}");

    [GeneratedRegex("[^\x00-\x7F]")]
    private static partial Regex NonAscii();
}
