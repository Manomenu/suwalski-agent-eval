namespace Suwageval.Cli.Init;

/// <summary>One top-level field of <c>agent.yaml</c> that <c>init</c> sets, and what it was (null: not in the file).</summary>
internal sealed record FieldChange(string Field, string? Old, string New);
