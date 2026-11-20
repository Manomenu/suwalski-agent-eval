using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

public sealed record ChangedCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    public required CheckChange Change { get; init; }

    public CheckInput? Before { get; init; }

    public CheckInput? After { get; init; }

    public required bool AfterJudgedByModel { get; init; }
}
