using System.Text.RegularExpressions;

namespace Suwageval.Contracts;

/// <summary>Keys and tags are lowercase kebab-case: <c>refund-happy-path</c>.</summary>
public static partial class Kebab
{
    public static bool IsValid(string text) => Pattern().IsMatch(text);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}
