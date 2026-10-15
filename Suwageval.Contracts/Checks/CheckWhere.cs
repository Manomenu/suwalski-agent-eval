namespace Suwageval.Contracts.Checks;

/// <summary>Narrows a check to a part of what the agent produced.</summary>
public sealed record CheckWhere
{
    public TurnSelector Turn { get; init; }

    /// <summary>A tool name or event type: only what came after it counts.</summary>
    public string? After { get; init; }

    /// <summary>A tool name or event type: only what came before it counts.</summary>
    public string? Before { get; init; }
}
