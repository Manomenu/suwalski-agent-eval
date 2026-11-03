namespace Suwageval.Contracts.Runs;

/// <summary>
/// The acceptance criterion a scenario guards (its <c>acGuardrail</c> in the revision that ran) and, in a gate run, how its attempts went.
/// </summary>
public sealed record ScenarioGuardrailResult
{
    /// <summary>The criterion's id, <c>AC-&lt;feature&gt;-&lt;n&gt;</c>.</summary>
    public required string Id { get; init; }

    /// <summary>In a gate run: the most attempts the scenario had (its own limit, else the agent's at the start of the run, else 5); none in another run.</summary>
    public int? RetryLimit { get; init; }

    /// <summary>In a gate run: the attempts made; none in another run.</summary>
    public int? Attempts { get; init; }

    /// <summary>In a gate run: true when an attempt passed every check, false when the attempts ran out; none in another run, or while attempts go on.</summary>
    public bool? Passed { get; init; }
}
