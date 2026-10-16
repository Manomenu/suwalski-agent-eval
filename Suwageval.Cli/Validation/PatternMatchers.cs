using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Cli.Validation;

/// <summary>
/// The <c>$pattern</c> matchers inside a check's <c>args</c>, <c>data</c> and <c>result</c> — an object whose only property is
/// <c>$pattern</c>, a string. The server evaluates them (<c>JsonSubset</c>); here only the regular expression is checked.
/// </summary>
internal static class PatternMatchers
{
    private const string PatternProperty = "$pattern";

    /// <summary>The matchers inside <paramref name="expected"/> whose regular expression does not compile: their path and why.</summary>
    public static IEnumerable<(string Path, string Error)> Invalid(JsonElement expected, string path)
    {
        if (expected.ValueKind == JsonValueKind.Object && expected.EnumerateObject().ToList() is [{ Name: PatternProperty } only])
        {
            return only.Value.ValueKind == JsonValueKind.String && Compile(only.Value.GetString() ?? "") is { } error
                ? [($"{path}.{PatternProperty}", error)]
                : [];
        }
        if (expected.ValueKind == JsonValueKind.Object)
        {
            return expected.EnumerateObject().SelectMany(property => Invalid(property.Value, $"{path}.{property.Name}"));
        }
        return expected.ValueKind == JsonValueKind.Array
            ? expected.EnumerateArray().SelectMany((item, index) => Invalid(item, $"{path}[{index}]"))
            : [];
    }

    private static string? Compile(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return null;
        }
        catch (ArgumentException error)
        {
            return error.Message;
        }
    }
}
