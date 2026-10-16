namespace Suwageval.Contracts.Samples;

/// <summary>
/// How many times a scenario's conversation is sent to the agent (<c>samples:</c>). It can be set on three levels, the most specific
/// one wins: a check, its scenario, the agent (<c>agent.yaml</c>); <see cref="Default"/> when none sets it.
/// </summary>
public sealed record SampleSettings
{
    /// <summary>The number of samples when neither the check, the scenario nor the agent sets one.</summary>
    public const int Default = 3;

    public const int Min = 1;

    public const int Max = 20;

    /// <summary>The samples a check is judged on: its own, else its scenario's, else the agent's, else <see cref="Default"/>.</summary>
    public static int Effective(int? check, int? scenario, int? agent) => check ?? scenario ?? agent ?? Default;
}
