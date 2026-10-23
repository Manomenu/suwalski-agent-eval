namespace Suwageval.AgentSdk.Invocation;

/// <summary>What kind of scenario a turn belongs to: one run can hold both.</summary>
public enum ScenarioType
{
    /// <summary>A recorded conversation: the agent answers its last message.</summary>
    Turn,

    /// <summary>A simulated user plays the conversation turn after turn.</summary>
    Simulation,
}
