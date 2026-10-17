using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents;

/// <summary>The outcome of deleting a version and, unless it was not found, what it holds (or held).</summary>
internal sealed record VersionDeletionResult(DeletionOutcome Outcome, VersionDeletion? Contents);
