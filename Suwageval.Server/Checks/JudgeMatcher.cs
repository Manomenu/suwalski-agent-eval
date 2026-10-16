using System.Text.Json;

namespace Suwageval.Server.Checks;

/// <summary>
/// The <c>$judge</c> matcher inside a check's expected <c>args</c> / <c>data</c>: an object whose ONLY property is <c>$judge</c> stands
/// for "a value like this" and is decided by a model, not here. This is the model-free half — recognising a matcher, reading and
/// validating its shape; the model is asked in <c>Judges/</c>. An object with <c>$judge</c> next to other properties is not a
/// matcher: validation rejects it, so a mistaken indentation is reported instead of being matched as a literal.
/// </summary>
internal static class JudgeMatcher
{
    public const string Key = "$judge";

    private const string Description = "description";
    private const string Examples = "examples";

    public static bool IsMatcher(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Count() == 1 && node.TryGetProperty(Key, out _);

    /// <summary>Whether a matcher — or the key of one — is anywhere in <paramref name="expected"/>.</summary>
    public static bool IsIn(JsonElement expected) =>
        expected.ValueKind switch
        {
            JsonValueKind.Object => expected.TryGetProperty(Key, out _) || expected.EnumerateObject().Any(property => IsIn(property.Value)),
            JsonValueKind.Array => expected.EnumerateArray().Any(IsIn),
            JsonValueKind.Undefined
            or JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null => false,
            _ => false,
        };

    /// <summary>The spec of a matcher node (<see cref="IsMatcher"/> is true); only valid after <see cref="Problems"/> found nothing.</summary>
    public static JudgeSpec Read(JsonElement matcher)
    {
        var spec = matcher.GetProperty(Key);
        return new JudgeSpec(
            spec.TryGetProperty(Description, out var description) ? description.GetString() : null,
            spec.TryGetProperty(Examples, out var examples) ? [.. examples.EnumerateArray().Select(example => example.Clone())] : []
        );
    }

    /// <summary>Everything wrong with the matchers in <paramref name="expected"/>, each with the path it was found at (under <paramref name="path"/>).</summary>
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
                if (!IsMatcher(node))
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
        foreach (var property in spec.EnumerateObject().Where(property => property.Name is not (Description or Examples)))
        {
            problems.Add((path, $"'{Key}' has no key '{property.Name}' (allowed: {Description}, {Examples})."));
        }
        var hasDescription = spec.TryGetProperty(Description, out var description);
        var hasExamples = spec.TryGetProperty(Examples, out var examples);
        if (!hasDescription && !hasExamples)
        {
            problems.Add((path, $"'{Key}' needs 'description' and/or 'examples'."));
        }
        if (hasDescription && (description.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(description.GetString())))
        {
            problems.Add(($"{path}.{Description}", "The description is a non-empty string."));
        }
        if (hasExamples && (examples.ValueKind != JsonValueKind.Array || examples.GetArrayLength() == 0))
        {
            problems.Add(($"{path}.{Examples}", "The examples are a non-empty list of values."));
        }
    }
}
