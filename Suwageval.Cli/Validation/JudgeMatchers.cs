using System.Text.Json;

namespace Suwageval.Cli.Validation;

/// <summary>
/// The shape of the <c>$judge</c> matchers in a check's <c>args</c> / <c>data</c> (docs/scenario-format.md): an object whose only property is
/// <c>$judge</c>, holding <c>description</c> (non-empty text) and/or <c>examples</c> (a non-empty list). The server applies the same rules to
/// a push (<c>Checks/JudgeMatcher.cs</c>; the two projects share nothing but the contracts, so the rules are written twice and tested twice).
/// </summary>
internal static class JudgeMatchers
{
    private const string Key = "$judge";

    /// <summary>What is wrong with the matchers in <paramref name="expected"/>, each with its path (under <paramref name="path"/>).</summary>
    public static List<(string Path, string Message)> Problems(JsonElement expected, string path)
    {
        var problems = new List<(string, string)>();
        Walk(expected, path, problems);
        return problems;
    }

    private static void Walk(JsonElement node, string path, List<(string, string)> problems)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object when node.TryGetProperty(Key, out var spec):
                if (node.EnumerateObject().Count() > 1)
                {
                    problems.Add((path, $"'{Key}' must be the only property of its object; this one has others."));
                }
                else
                {
                    CheckSpec(spec, $"{path}.{Key}", problems);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    Walk(property.Value, $"{path}.{property.Name}", problems);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var element in node.EnumerateArray())
                {
                    Walk(element, $"{path}[{index++}]", problems);
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

    private static void CheckSpec(JsonElement spec, string path, List<(string, string)> problems)
    {
        if (spec.ValueKind != JsonValueKind.Object)
        {
            problems.Add((path, $"'{Key}' is an object with 'description' and/or 'examples'."));
            return;
        }
        foreach (var property in spec.EnumerateObject().Where(property => property.Name is not ("description" or "examples")))
        {
            problems.Add((path, $"'{Key}' has no key '{property.Name}' (allowed: description, examples)."));
        }
        var hasDescription = spec.TryGetProperty("description", out var description);
        var hasExamples = spec.TryGetProperty("examples", out var examples);
        if (!hasDescription && !hasExamples)
        {
            problems.Add((path, $"'{Key}' needs 'description' and/or 'examples'."));
        }
        if (hasDescription && (description.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(description.GetString())))
        {
            problems.Add(($"{path}.description", "The description is a non-empty string."));
        }
        if (hasExamples && (examples.ValueKind != JsonValueKind.Array || examples.GetArrayLength() == 0))
        {
            problems.Add(($"{path}.examples", "The examples are a non-empty list of values."));
        }
    }
}
