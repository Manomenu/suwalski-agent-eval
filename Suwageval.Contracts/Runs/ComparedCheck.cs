using Suwageval.Contracts.Checks;

namespace Suwageval.Contracts.Runs;

public sealed record ComparedCheck
{
    public required string ScenarioKey { get; init; }

    public required string CheckKey { get; init; }

    public required IReadOnlyList<string> ScenarioTags { get; init; }

    public required CheckInput Definition { get; init; }

    public required bool JudgedByModel { get; init; }

    public required bool Differs { get; init; }

    public required bool Comparable { get; init; }

    public required IReadOnlyList<ComparedCell?> Cells { get; init; }
}
