using Suwageval.Contracts.Agents;

namespace Suwageval.Cli.Files;

internal sealed record LoadedAgent(YamlSource Source, AgentDefinition Definition, AgentVersionInput Version);
