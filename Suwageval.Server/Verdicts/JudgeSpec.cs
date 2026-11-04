using System.Text.Json;

namespace Suwageval.Server.Verdicts;

internal sealed record JudgeSpec(string? Description, IReadOnlyList<JsonElement> Examples);
