using System.Text.Json;

namespace Suwageval.Server.Invocation;

/// <summary>
/// What goes with a turn besides the conversation: the scenario's fixtures (sent in the body) and where the call comes from
/// (sent as the headers <c>Suwageval-Run-Id</c>, <c>Suwageval-Scenario</c>, <c>Suwageval-Sample</c>, for the agent's traces and
/// logs — they never change behaviour).
/// </summary>
internal sealed record TurnContext(Guid RunId, string ScenarioKey, int Sample, JsonElement? Fixtures = null);
