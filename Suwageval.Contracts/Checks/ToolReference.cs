namespace Suwageval.Contracts.Checks;

/// <summary>A tool a check names, and the agent it asks for it (null: any agent).</summary>
public sealed record ToolReference(string Tool, string? By);
