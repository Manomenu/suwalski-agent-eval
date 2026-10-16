namespace Suwageval.Contracts.Checks;

public sealed record CheckResult(string Key, UpsertOutcome Outcome);
