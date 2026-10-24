using System.Globalization;

namespace Suwageval.Cli.Evaluation.Sweep;

/// <summary>The numbered list <c>suwageval sweep --select</c> shows and the reading of the answer to it.</summary>
internal static class VariantSelection
{
    public static string Menu(IReadOnlyList<string> variants) =>
        string.Join(Environment.NewLine, variants.Select((variant, index) => $"  {index + 1}) {variant}"));

    /// <summary>
    /// The variants an answer picks, in the list's order and without repeats: <c>all</c>, or numbers and ranges (<c>1,3</c>, <c>2-4</c>)
    /// separated by commas or spaces. Null with <paramref name="problem"/> when the answer picks nothing or something not in the list.
    /// </summary>
    public static List<string>? Parse(string answer, IReadOnlyList<string> variants, out string problem)
    {
        problem = "";
        var parts = answer.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            problem = "Nothing selected: type numbers like 1,3 or all.";
            return null;
        }
        if (parts.Length == 1 && parts[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return [.. variants];
        }
        var picked = new SortedSet<int>();
        foreach (var part in parts)
        {
            var bounds = part.Split('-');
            if (
                bounds.Length > 2
                || !int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
                || !int.TryParse(bounds[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            )
            {
                problem = $"'{part}' is not a number or a range like 2-4.";
                return null;
            }
            if (first < 1 || last > variants.Count || first > last)
            {
                problem = $"'{part}' is outside 1-{variants.Count}.";
                return null;
            }
            picked.UnionWith(Enumerable.Range(first, last - first + 1));
        }
        return [.. picked.Select(number => variants[number - 1])];
    }
}
