using System.Text.Json;

namespace Suwageval.Server.Checks.Evaluation;

/// <summary>What a <c>$judge</c> matcher asks of a value (docs/scenario-format.md, "The <c>$judge</c> matcher"): at least one of the two is given.</summary>
internal sealed record JudgeSpec(string? Description, IReadOnlyList<JsonElement> Examples);
