using Suwageval.AgentSdk.Agents;
using Suwageval.AgentSdk.Conversations;
using Suwageval.Cli.Files;
using Suwageval.Contracts;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Samples;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Cli.Validation;

/// <summary>
/// Everything that can be said about an agent's files: they match the contract, and what they
/// say about each other holds (keys, tool results; tools and events too, once the agent's own contract is at hand).
/// </summary>
internal static class Validator
{
    public static ValidationResult Validate(AgentFolder folder, AgentContract? contract = null) =>
        Validate(folder.AgentFile, folder.AgentText, folder.Scenarios, contract);

    /// <param name="agentFile">Name of the agent file, for the problem when <paramref name="agent"/> is missing.</param>
    /// <param name="agent">The agent file, or null when it does not exist.</param>
    /// <param name="scenarioFiles">The scenario files to check, each by its name and content.</param>
    /// <param name="contract">What the running agent serves: with it tool and event names are checked, without it they are not.</param>
    public static ValidationResult Validate(
        string agentFile,
        string? agent,
        IReadOnlyList<SourceText> scenarioFiles,
        AgentContract? contract = null
    )
    {
        var problems = new List<Problem>();
        LoadedAgent? loadedAgent = null;
        if (agent is null)
        {
            problems.Add(new Problem(agentFile, "", "The agent file does not exist.", null, null));
        }
        else
        {
            loadedAgent = AgentFileReader.Read(agentFile, agent, problems);
        }

        var scenarioKeys = new Dictionary<string, string>();
        var checkKeys = new Dictionary<string, string>();
        var checkCount = 0;
        var loadedScenarios = new List<LoadedScenario>();
        foreach (var file in scenarioFiles)
        {
            var loaded = ScenarioFileReader.Read(file.File, file.Text, problems);
            if (loaded is null)
            {
                continue;
            }
            loadedScenarios.Add(loaded);
            checkCount += loaded.Checks.Count;
            CheckScenario(loaded, contract, scenarioKeys, checkKeys, problems);
        }
        return new ValidationResult(problems, loadedScenarios.Count, checkCount) { Agent = loadedAgent, LoadedScenarios = loadedScenarios };
    }

    private static void CheckScenario(
        LoadedScenario loaded,
        AgentContract? contract,
        Dictionary<string, string> scenarioKeys,
        Dictionary<string, string> checkKeys,
        List<Problem> problems
    )
    {
        var source = loaded.Source;
        var scenario = loaded.Scenario;

        CheckKey(source, "key", scenario.Key, scenarioKeys, "scenario", problems);
        for (var i = 0; i < scenario.Tags.Count; i++)
        {
            if (!Kebab.IsValid(scenario.Tags[i]))
            {
                problems.Add(
                    source.Problem($"tags[{i}]", $"The tag '{scenario.Tags[i]}' is not lowercase kebab-case (like 'refund-flow').")
                );
            }
        }
        if (SampleRules.Problem(scenario.Samples, "samples") is { } samplesProblem)
        {
            problems.Add(source.Problem("samples", samplesProblem));
        }
        foreach (var (path, message) in ContinuationRules.Problems(scenario))
        {
            problems.Add(source.Problem(path, message));
        }
        if (scenario.Conversation.Count == 0)
        {
            problems.Add(source.Problem("conversation", "The conversation has no items; the agent needs a message to answer."));
        }

        if (scenario.Fixtures is not null && contract is { Context.FixturesSchema: null })
        {
            problems.Add(
                source.Problem("fixtures", "The scenario has fixtures, but the agent's contract declares no context.fixturesSchema.")
            );
        }

        CheckConversation(loaded, contract, problems);

        for (var i = 0; i < loaded.Checks.Count; i++)
        {
            CheckCheck(source, loaded.Checks[i], $"checks[{i}]", contract, checkKeys, problems);
            if (ContinuationRules.CheckProblem(loaded.Checks[i], scenario.Continuation) is { } turnProblem)
            {
                problems.Add(source.Problem($"checks[{i}].where.turn", turnProblem));
            }
        }
    }

    private static void CheckConversation(LoadedScenario loaded, AgentContract? contract, List<Problem> problems)
    {
        var source = loaded.Source;
        var callIds = new HashSet<string>();
        for (var i = 0; i < loaded.Scenario.Conversation.Count; i++)
        {
            var path = $"conversation[{i}]";
            switch (loaded.Scenario.Conversation[i])
            {
                case ToolCall call:
                    callIds.Add(call.Id);
                    CheckTool(source, call.Agent is null ? $"{path}.name" : $"{path}.agent", call.Name, call.Agent, contract, problems);
                    break;
                case ToolResult result when !callIds.Contains(result.CallId):
                    problems.Add(source.Problem($"{path}.callId", $"No earlier tool_call has the id '{result.CallId}'."));
                    break;
                case Retrieval when contract is { Context.UsesRetrieval: false }:
                    problems.Add(source.Problem(path, "A retrieval item needs 'usesRetrieval: true' in the agent's contract."));
                    break;
                case Event conversationEvent when contract is not null && !EventTypes(contract).Contains(conversationEvent.Type):
                    problems.Add(source.Problem($"{path}.type", UnknownEvent(conversationEvent.Type, contract)));
                    break;
                default:
                    break;
            }
        }
    }

    private static void CheckCheck(
        YamlSource source,
        CheckInput check,
        string path,
        AgentContract? contract,
        Dictionary<string, string> checkKeys,
        List<Problem> problems
    )
    {
        problems.AddRange(CheckRules.Problems(check, path).Select(problem => source.Problem(problem.Path, problem.Message)));
        CheckUnique(source, $"{path}.key", check.Key, checkKeys, "check", problems);
        if (contract is not null)
        {
            if (CheckRules.NamedTool(check) is { } named)
            {
                CheckTool(source, named.By is null ? $"{path}.tool" : $"{path}.by", named.Tool, named.By, contract, problems);
            }
            if (CheckRules.NamedEvent(check) is { } eventType)
            {
                CheckEvent(source, $"{path}.event", eventType, contract, problems);
            }
            foreach (var (anchorPath, name) in CheckRules.Anchors(check))
            {
                CheckAnchor(source, $"{path}.{anchorPath}", name, contract, problems);
            }
        }
    }

    private static void CheckKey(
        YamlSource source,
        string path,
        string key,
        Dictionary<string, string> seen,
        string what,
        List<Problem> problems
    )
    {
        if (!Kebab.IsValid(key))
        {
            problems.Add(source.Problem(path, $"The {what} key '{key}' is not lowercase kebab-case (like 'refund-happy-path')."));
        }
        CheckUnique(source, path, key, seen, what, problems);
    }

    private static void CheckUnique(
        YamlSource source,
        string path,
        string key,
        Dictionary<string, string> seen,
        string what,
        List<Problem> problems
    )
    {
        if (!seen.TryAdd(key, source.File))
        {
            problems.Add(source.Problem(path, $"The {what} key '{key}' is already used in {seen[key]}."));
        }
    }

    /// <summary>The tool must exist: on the named agent, or, with none named, on some agent.</summary>
    private static void CheckTool(
        YamlSource source,
        string path,
        string tool,
        string? agent,
        AgentContract? contract,
        List<Problem> problems
    )
    {
        if (contract is null)
        {
            return;
        }
        if (agent is null)
        {
            if (!contract.Agents.Any(declared => declared.Tools.Any(t => t.Name == tool)))
            {
                var known = string.Join(", ", AllTools(contract));
                problems.Add(source.Problem(path, $"No agent has the tool '{tool}' (the tools: {known})."));
            }
            return;
        }
        var owner = contract.Agents.FirstOrDefault(declared => declared.Name == agent);
        if (owner is null)
        {
            var known = string.Join(", ", contract.Agents.Select(declared => declared.Name));
            problems.Add(source.Problem(path, $"The system has no agent '{agent}' (its agents: {known})."));
        }
        else if (owner.Tools.All(t => t.Name != tool))
        {
            var known = string.Join(", ", owner.Tools.Select(t => t.Name));
            problems.Add(source.Problem(path, $"The agent '{agent}' has no tool '{tool}' (its tools: {known})."));
        }
    }

    private static IEnumerable<string> AllTools(AgentContract contract) =>
        contract.Agents.SelectMany(agent => agent.Tools.Select(tool => tool.Name)).Distinct();

    private static void CheckAnchor(YamlSource source, string path, string? name, AgentContract contract, List<Problem> problems)
    {
        if (name is not null && !AllTools(contract).Contains(name) && !EventTypes(contract).Contains(name))
        {
            problems.Add(source.Problem(path, $"'{name}' is neither a tool of the agent nor one of its event types."));
        }
    }

    private static void CheckEvent(YamlSource source, string path, string type, AgentContract? contract, List<Problem> problems)
    {
        if (contract is not null && !EventTypes(contract).Contains(type))
        {
            problems.Add(source.Problem(path, UnknownEvent(type, contract)));
        }
    }

    private static HashSet<string> EventTypes(AgentContract? contract) =>
        [.. contract?.Context.Events.Select(declared => declared.Type) ?? []];

    private static string UnknownEvent(string type, AgentContract? contract)
    {
        var known = string.Join(", ", EventTypes(contract));
        return $"The agent does not declare the event type '{type}' in its contract (declared: {(known.Length == 0 ? "none" : known)}).";
    }
}
