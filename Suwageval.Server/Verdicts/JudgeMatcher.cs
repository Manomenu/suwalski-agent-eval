using System.Text.Json;
using Suwageval.Contracts.Checks;

namespace Suwageval.Server.Verdicts;

// Why: an object with $judge next to other properties is not a matcher, so a mistaken indentation is reported instead of matched as a literal
internal static class JudgeMatcher
{
    private const string Key = CheckRules.JudgeKey;

    private const string Description = "description";
    private const string Examples = "examples";

    public static bool IsMatcher(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Count() == 1 && node.TryGetProperty(Key, out _);

    public static JudgeSpec Read(JsonElement matcher)
    {
        var spec = matcher.GetProperty(Key);
        return new JudgeSpec(
            spec.TryGetProperty(Description, out var description) ? description.GetString() : null,
            spec.TryGetProperty(Examples, out var examples) ? [.. examples.EnumerateArray().Select(example => example.Clone())] : []
        );
    }
}
