namespace Suwageval.Contracts.Runs;

/// <summary>A run and how far it is. A sample is one scenario run once; the checks are counted per sample.</summary>
public sealed record RunSummary
{
    public required Guid Id { get; init; }

    public required string Agent { get; init; }

    public required Guid AgentVersionId { get; init; }

    public string? Tag { get; init; }

    public required string EndpointUrl { get; init; }

    public required RunStatus Status { get; init; }

    /// <summary>Why a <see cref="RunStatus.Failed"/> run failed.</summary>
    public string? Error { get; init; }

    /// <summary>How many samples the run will produce; zero until the run starts.</summary>
    public required int SamplesTotal { get; init; }

    public required int SamplesDone { get; init; }

    /// <summary>How many of the done samples have no agent answer (a timeout, an HTTP error, an unreadable answer).</summary>
    public int SamplesFailed { get; init; }

    /// <summary>Checks that reached their pass threshold over their samples so far, as the checks of the results do.</summary>
    public required int ChecksPassed { get; init; }

    public required int ChecksFailed { get; init; }

    /// <summary>
    /// <c>true</c> when the agent's contract, read again at the end of the run, differs from the version the run started with
    /// (a redeploy mid-run): the results may not all be of that version.
    /// </summary>
    public bool ContractChangedDuringRun { get; init; }

    /// <summary>
    /// <c>true</c> once a stop was asked for. A run still <see cref="RunStatus.Running"/> with this set is finishing the samples in
    /// flight; it becomes <see cref="RunStatus.Cancelled"/> when they are done.
    /// </summary>
    public bool CancelRequested { get; init; }

    public required DateTime CreatedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    /// <summary>
    /// The run whose stored answers this run evaluated again with the current checks, without calling the agent (a re-check); null for a
    /// run that called the agent. Always a run that called the agent: a re-check of a re-check points at the original. The turn times
    /// of a re-check are the source's, copied, not measured again.
    /// </summary>
    public Guid? RecheckOf { get; init; }

    /// <summary>The scenarios of the source a re-check left out, with the reason; empty for a run that called the agent.</summary>
    public IReadOnlyList<SkippedScenario> SkippedScenarios { get; init; } = [];

    /// <summary>Where the agent's repository stood when the run was started; a rerun and a re-check have their source's.</summary>
    public RunOrigin? Origin { get; init; }

    /// <summary>True when the run makes the history of its branch (a branch of the agent's regression settings, a clean tree, a pushed commit).</summary>
    public bool Reference { get; init; }

    /// <summary>The branch whose history the run is compared with; none means every run of the agent (a run without git).</summary>
    public string? ComparedWith { get; init; }

    /// <summary>The distinct models that gave the run's verdicts, sorted by name; empty for a run with no model-judged check (or one made before the name was recorded).</summary>
    public IReadOnlyList<string> JudgeModels { get; init; } = [];
}
