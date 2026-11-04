using System.Text.Json;
using System.Text.RegularExpressions;

namespace Suwageval.Server.Verdicts;

internal static class JsonSubset
{
    private const string PatternProperty = "$pattern";
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    // Why: a pattern that is invalid or takes over a second matches nothing, and an object with both $pattern and $judge is neither matcher
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

    private static bool SameNumber(JsonElement actual, JsonElement expected) =>
        actual.TryGetDecimal(out var a) && expected.TryGetDecimal(out var e) ? a == e : actual.GetDouble().Equals(expected.GetDouble());
}
