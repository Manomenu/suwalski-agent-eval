using Suwageval.Contracts.Runs;
using Suwageval.Server.Agents;

namespace Suwageval.Server.Runs.Groups;

/// <summary>The outcome of deleting a run group and, unless it was not found, what it holds (or held).</summary>
internal sealed record RunGroupDeletionResult(DeletionOutcome Outcome, RunGroupDeletion? Contents);
