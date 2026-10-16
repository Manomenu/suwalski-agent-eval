using System.Text.Json;

namespace Suwageval.Server.Checks;

/// <summary>"Is this JSON a subset of that one" — how a check's <c>args</c> and <c>data</c> are matched.</summary>
internal static class JsonSubset
{
    /// <summary>
    /// True when <paramref name="actual"/> contains <paramref name="expected"/>: an object has every property of the expected one
    /// (extra properties are fine, values matched the same way, recursively); an array has the same length and each element
    /// contains the expected element at that position; strings, numbers (by value, so 1 equals 1.0), booleans and null are equal.
    /// </summary>
    public static bool Contains(JsonElement actual, JsonElement expected) =>
        expected.ValueKind switch
        {
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

    /// <summary>By value; numbers too large for a decimal fall back to double.</summary>
    private static bool SameNumber(JsonElement actual, JsonElement expected) =>
        actual.TryGetDecimal(out var a) && expected.TryGetDecimal(out var e) ? a == e : actual.GetDouble().Equals(expected.GetDouble());
}
