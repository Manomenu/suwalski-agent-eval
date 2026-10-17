using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

/// <param name="Source"></param>
/// <param name="Definition">What is pushed to the server.</param>
/// <param name="SuwagevalUrl">The file's own <c>suwagevalUrl</c>: where Suwageval is, for the CLI only, never sent.</param>
internal sealed record LoadedAgent(YamlSource Source, AgentDefinition Definition, string? SuwagevalUrl);
