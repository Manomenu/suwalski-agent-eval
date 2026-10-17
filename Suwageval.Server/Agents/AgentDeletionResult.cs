using Suwageval.Contracts.Agents;

namespace Suwageval.Server.Agents;

/// <summary>The outcome of deleting an agent and, unless it was not found, what it holds (or held).</summary>
internal sealed record AgentDeletionResult(DeletionOutcome Outcome, AgentDeletion? Contents);
