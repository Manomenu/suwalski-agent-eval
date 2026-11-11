using System.Text.Json;

namespace Suwageval.AgentSdk.Agents;

/// <summary>What besides messages and tool calls may appear in the agent's conversation log.</summary>
public sealed record ContextDefinition
{
    public bool UsesRetrieval { get; init; }

    /// <summary>The version of the data retrieval reads: required with <c>UsesRetrieval</c>, absent without it.</summary>
    public RetrievalDefinition? Retrieval { get; init; }

    public IReadOnlyList<EventDeclaration> Events { get; init; } = [];

    /// <summary>A JSON Schema of the scenario fixtures: present means the agent accepts them.</summary>
    public JsonElement? FixturesSchema { get; init; }

    /// <summary>
    /// The tools the system's own code calls, with no LLM agent choosing them (a deterministic step that books a visit, say). No
    /// agent owns them, so a <c>ToolCall</c> of one names no <c>Agent</c>; a name is either here or an agent's
    /// tool, never both (<see cref="AgentContractRules"/>). Part of the content hash; an agent may leave it out when empty, and an
    /// empty list is not hashed, so a contract without them is the version it was before they existed.
    /// </summary>
    [OmittedWhenEmpty]
    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
}
