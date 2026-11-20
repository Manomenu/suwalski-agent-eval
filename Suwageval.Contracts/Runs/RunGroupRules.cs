namespace Suwageval.Contracts.Runs;

public static class RunGroupRules
{
    public const int MaxLength = 64;

    public static string? Problem(string? group) =>
        group is null || (group.Length <= MaxLength && Kebab.IsValid(group))
            ? null
            : $"The group is lowercase kebab-case (letters, digits and single dashes, like 'sweep-2026-10-21'), at most {MaxLength} characters.";
}
