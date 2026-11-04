namespace Suwageval.Server.Agents;

/// <summary>A refusal the client sees as 409: its title and what to do.</summary>
internal sealed record DeletionRefusal(string Title, string Detail);
