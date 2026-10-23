namespace Suwageval.Cli.Output;

/// <summary>Where the CLI can ask the person running it a question: the input they type into, and whether anyone is there to type.</summary>
/// <param name="Input">What the person types.</param>
/// <param name="Interactive">False when the input is redirected (a CI job, a pipe): a question would wait for an answer that never comes.</param>
internal sealed record Terminal(TextReader Input, bool Interactive);
