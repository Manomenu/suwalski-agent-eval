namespace Suwageval.Contracts.Runs;

/// <summary>What a run's group may be called: lowercase kebab-case like keys and tags, at most <see cref="MaxLength"/> characters.</summary>
public static class RunGroupRules
{
    public const int MaxLength = 64;

    /// <summary>What is wrong with <paramref name="group"/>, or null when it is fine (or none).</summary>
    public static string? Problem(string? group) =>
        group is null || (group.Length <= MaxLength && Kebab.IsValid(group))
            ? null
            : $"The group is lowercase kebab-case (letters, digits and single dashes, like 'sweep-2026-10-21'), at most {MaxLength} characters.";
}
