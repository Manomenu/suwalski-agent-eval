using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

/// <param name="Source"></param>
/// <param name="Definition">What is pushed to the server.</param>
/// <param name="Sweep">The file's own <c>sweep</c> section, for <c>suwageval sweep</c> only, never sent.</param>
/// <param name="SuwagevalUrl">The file's own <c>suwagevalUrl</c>: where Suwageval is, for the CLI only, never sent.</param>
internal sealed record LoadedAgent(YamlSource Source, AgentDefinition Definition, string? SuwagevalUrl, SweepSettings? Sweep = null);
