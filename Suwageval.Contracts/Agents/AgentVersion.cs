namespace Suwageval.Contracts.Agents;

/// <summary>
/// A registered version of an agent, without its content (but with the names of the models its LLM agents use, so runs can be
/// filtered by model, the version of its retrieval data and its description, if it has any);
/// <c>ArchivedAt</c> is set while the version is archived.
/// </summary>
public sealed record AgentVersion(
    Guid Id,
    Guid AgentId,
    string? Label,
    string ContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<string> ModelNames,
    string? DataVersion,
    string? DataDescription
);
