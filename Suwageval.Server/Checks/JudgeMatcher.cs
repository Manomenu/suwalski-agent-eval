using System.Text.Json;
using Suwageval.Contracts.Checks;

namespace Suwageval.Server.Checks;

/// <summary>
/// The <c>$judge</c> matcher inside a check's expected <c>args</c> / <c>data</c>: an object whose ONLY property is <c>$judge</c> stands
/// for "a value like this" and is decided by a model, not here. This is the model-free half — recognising a matcher and reading it; the model
/// is asked in <c>Judges/</c>. Whether a matcher is well formed is <c>CheckRules</c>' to say; an object with <c>$judge</c> next to other
/// properties is not a matcher and validation rejects it, so a mistaken indentation is reported instead of being matched as a literal.
/// </summary>
internal static class JudgeMatcher
{
    private const string Key = CheckRules.JudgeKey;

    private const string Description = "description";
    private const string Examples = "examples";

    public static bool IsMatcher(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Count() == 1 && node.TryGetProperty(Key, out _);

    /// <summary>The spec of a matcher node (<see cref="IsMatcher"/> is true); only valid after <see cref="CheckRules.Problems"/> found nothing.</summary>
    public static JudgeSpec Read(JsonElement matcher)
    {
        var spec = matcher.GetProperty(Key);
        return new JudgeSpec(
            spec.TryGetProperty(Description, out var description) ? description.GetString() : null,
            spec.TryGetProperty(Examples, out var examples) ? [.. examples.EnumerateArray().Select(example => example.Clone())] : []
        );
    }
}
