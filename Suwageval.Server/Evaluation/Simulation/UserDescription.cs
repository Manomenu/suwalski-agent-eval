namespace Suwageval.Server.Evaluation.Simulation;

/// <summary>
/// A simulated user's description drafted from a real conversation: <see cref="Text"/> to review, and <see cref="Notice"/> when the model
/// did not write it (none configured, no answer) and the text is the plain fallback built from the user's messages.
/// </summary>
internal sealed record UserDescription(string Text, string? Notice = null);
