namespace Suwageval.Contracts.Agents;

/// <summary>Retrieval data a version's runs ran on: its <c>dataVersion</c> and what the agent said it is (the newest description given).</summary>
public sealed record RetrievalData(string Version, string? Description = null);
