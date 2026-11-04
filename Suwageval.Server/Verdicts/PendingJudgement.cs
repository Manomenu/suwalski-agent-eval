using System.Text.Json;

namespace Suwageval.Server.Verdicts;

internal sealed record PendingJudgement(string Path, JsonElement Actual, JudgeSpec Spec);
