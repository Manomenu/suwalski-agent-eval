using System.Text.Json;

namespace Suwageval.Server.Checks;

/// <summary>"Is this JSON a subset of that one" — how a check's <c>args</c> and <c>data</c> are matched.</summary>
internal static class JsonSubset
{
    /// <summary>
    /// True when <paramref name="actual"/> contains <paramref name="expected"/>: an object has every property of the expected one
    /// (extra properties are fine, values matched the same way, recursively); an array has the same length and each element
    /// contains the expected element at that position; strings, numbers (by value, so 1 equals 1.0), booleans and null are equal.
    /// A <see cref="JudgeMatcher"/> node only needs a value to be there (any JSON): a model decides the rest, later.
    /// </summary>
    public static bool Contains(JsonElement actual, JsonElement expected) =>
        expected.ValueKind switch
        {
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
