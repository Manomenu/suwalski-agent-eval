using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Server.Checks;

/// <summary>"Is this JSON a subset of that one" — how a check's <c>args</c>, <c>data</c> and <c>result</c> are matched.</summary>
internal static class JsonSubset
{
    private const string PatternProperty = "$pattern";
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// True when <paramref name="actual"/> contains <paramref name="expected"/>: an object has every property of the expected one
    /// (extra properties are fine, values matched the same way, recursively); an array has the same length and each element
    /// contains the expected element at that position; strings, numbers (by value, so 1 equals 1.0), booleans and null are equal.
    /// An expected object whose only property is <c>$pattern</c> (a string) is a matcher: it is satisfied by an actual string
    /// the regular expression matches (a pattern that is invalid or takes over a second matches nothing).
    /// A <see cref="JudgeMatcher"/> node only needs a value to be there (any JSON): a model decides the rest, later.
    /// Both matchers need to be the ONLY property of their object; an object with both is neither (validation rejects it).
    /// </summary>
    public static bool Contains(JsonElement actual, JsonElement expected) =>
        expected.ValueKind switch
        {
            JsonValueKind.Object when PatternOf(expected) is { } pattern => actual.ValueKind == JsonValueKind.String
                && MatchesPattern(actual.GetString() ?? "", pattern),
            JsonValueKind.Object when JudgeMatcher.IsMatcher(expected) => true,
            JsonValueKind.Object => actual.ValueKind == JsonValueKind.Object
                && expected
                    .EnumerateObject()
                    .All(property => actual.TryGetProperty(property.Name, out var value) && Contains(value, property.Value)),
            JsonValueKind.Array => actual.ValueKind == JsonValueKind.Array
                && actual.GetArrayLength() == expected.GetArrayLength()
                && actual.EnumerateArray().Zip(expected.EnumerateArray()).All(pair => Contains(pair.First, pair.Second)),
            JsonValueKind.String => actual.ValueKind == JsonValueKind.String && actual.GetString() == expected.GetString(),
            JsonValueKind.Number => actual.ValueKind == JsonValueKind.Number && SameNumber(actual, expected),
            JsonValueKind.Undefined or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => actual.ValueKind
                == expected.ValueKind,
            _ => actual.ValueKind == expected.ValueKind,
        };

    /// <summary>The regular expression of a <c>{ "$pattern": "..." }</c> matcher; null when the object is anything else.</summary>
    public static string? PatternOf(JsonElement expected)
    {
        if (expected.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return expected.EnumerateObject().ToList() is [{ Name: PatternProperty, Value.ValueKind: JsonValueKind.String } only]
            ? only.Value.GetString()
            : null;
    }

    private static bool MatchesPattern(string text, string pattern)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, PatternTimeout);
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// The matchers inside <paramref name="expected"/> whose regular expression does not compile: the path (<c>args.zipCode</c>,
    /// <c>args.items[0]</c>) and why. Empty when every pattern is valid.
    /// </summary>
    public static IEnumerable<(string Path, string Error)> InvalidPatterns(JsonElement expected, string path)
    {
        if (PatternOf(expected) is { } pattern)
        {
            var error = Compile(pattern);
            return error is null ? [] : [($"{path}.{PatternProperty}", error)];
        }
        if (expected.ValueKind == JsonValueKind.Object)
        {
            return expected.EnumerateObject().SelectMany(property => InvalidPatterns(property.Value, $"{path}.{property.Name}"));
        }
        return expected.ValueKind == JsonValueKind.Array
            ? expected.EnumerateArray().SelectMany((item, index) => InvalidPatterns(item, $"{path}[{index}]"))
            : [];
    }

    private static string? Compile(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, PatternTimeout);
            return null;
        }
        catch (ArgumentException error)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// The <c>$judge</c> nodes of <paramref name="expected"/> with the values <paramref name="actual"/> has there, in document order;
    /// <paramref name="path"/> names the root (<c>args</c>). Call it only when <see cref="Contains"/> is true.
    /// </summary>
    public static List<PendingJudgement> Judgements(JsonElement actual, JsonElement expected, string path)
    {
        var found = new List<PendingJudgement>();
        Collect(actual, expected, path, found);
        return found;
    }

    private static void Collect(JsonElement actual, JsonElement expected, string path, List<PendingJudgement> found)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object when PatternOf(expected) is not null:
                break;
            case JsonValueKind.Object when JudgeMatcher.IsMatcher(expected):
                found.Add(new PendingJudgement(path, actual.Clone(), JudgeMatcher.Read(expected)));
                break;
            case JsonValueKind.Object:
                foreach (var property in expected.EnumerateObject())
                {
                    Collect(actual.GetProperty(property.Name), property.Value, $"{path}.{property.Name}", found);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var (element, expectedElement) in actual.EnumerateArray().Zip(expected.EnumerateArray()))
                {
                    Collect(element, expectedElement, $"{path}[{index++}]", found);
                }
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                break;
        }
    }

    /// <summary>By value; numbers too large for a decimal fall back to double.</summary>
    private static bool SameNumber(JsonElement actual, JsonElement expected) =>
        actual.TryGetDecimal(out var a) && expected.TryGetDecimal(out var e) ? a == e : actual.GetDouble().Equals(expected.GetDouble());
}
