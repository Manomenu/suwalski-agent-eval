using System.Text.Json;

namespace Suwageval.Server.Verdicts;

/// <summary>One <c>$judge</c> node: where it is (<c>args.reason</c>, <c>data.items[0].note</c>), the value found there and what is asked of it.</summary>
internal sealed record PendingJudgement(string Path, JsonElement Actual, JudgeSpec Spec);
