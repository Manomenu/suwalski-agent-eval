using System.Text.RegularExpressions;
using Suwageval.Cli.Files;
using Suwageval.Contracts;
using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Conversations;

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
        if (agent is null)
        {
            problems.Add(new Problem(agentFile, "", "The agent file does not exist.", null, null));
        }
        else
        {
            AgentFileReader.Read(agentFile, agent, problems);
        }

        var scenarioKeys = new Dictionary<string, string>();
        var checkKeys = new Dictionary<string, string>();
        var checkCount = 0;
        var scenarioCount = 0;
        foreach (var file in scenarioFiles)
        {
            var loaded = ScenarioFileReader.Read(file.File, file.Text, problems);
            if (loaded is null)
            {
                continue;
            }
            scenarioCount++;
            checkCount += loaded.Checks.Count;
            CheckScenario(loaded, contract, scenarioKeys, checkKeys, problems);
        }
        return new ValidationResult(problems, scenarioCount, checkCount);
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
        if (scenario.Samples < 1)
        {
            problems.Add(source.Problem("samples", "samples is at least 1."));
        }
        if (scenario.Continuation.Turns < 1)
        {
            problems.Add(source.Problem("continuation.turns", "turns is at least 1."));
        }
        if (scenario.Conversation.Count == 0)
        {
            problems.Add(source.Problem("conversation", "The conversation has no items; the agent needs a message to answer."));
        }

        CheckConversation(loaded, contract, problems);

        for (var i = 0; i < loaded.Checks.Count; i++)
        {
            CheckCheck(source, loaded.Checks[i], $"checks[{i}]", contract, checkKeys, problems);
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
        CheckKey(source, $"{path}.key", check.Key, checkKeys, "check", problems);
        if (check.PassThreshold is <= 0 or > 1 || double.IsNaN(check.PassThreshold))
        {
            problems.Add(source.Problem($"{path}.passThreshold", "passThreshold is above 0 and at most 1."));
        }

        switch (check)
        {
            case ToolCalledCheck called:
                CheckTool(source, $"{path}.tool", called.Tool, contract, problems);
                break;
            case ToolNotCalledCheck notCalled:
                CheckTool(source, $"{path}.tool", notCalled.Tool, contract, problems);
                break;
            case EventCheck eventCheck when contract is not null && !EventTypes(contract).Contains(eventCheck.Event):
                problems.Add(source.Problem($"{path}.event", UnknownEvent(eventCheck.Event, contract)));
                break;
            case TextCheck text:
                CheckText(source, path, text, problems);
                break;
            default:
                break;
        }

        if (check.Where is { } where && contract is not null)
        {
            CheckAnchor(source, $"{path}.where.after", where.After, contract, problems);
            CheckAnchor(source, $"{path}.where.before", where.Before, contract, problems);
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
        if (!seen.TryAdd(key, source.File))
        {
            problems.Add(source.Problem(path, $"The {what} key '{key}' is already used in {seen[key]}."));
        }
    }

    private static void CheckTool(YamlSource source, string path, string tool, AgentContract? contract, List<Problem> problems)
    {
        if (contract is not null && contract.Tools.All(declared => declared.Name != tool))
        {
            var known = string.Join(", ", contract.Tools.Select(declared => declared.Name));
            problems.Add(source.Problem(path, $"The agent has no tool '{tool}' (its tools: {known})."));
        }
    }

    private static void CheckAnchor(YamlSource source, string path, string? name, AgentContract contract, List<Problem> problems)
    {
        if (name is not null && contract.Tools.All(tool => tool.Name != name) && !EventTypes(contract).Contains(name))
        {
            problems.Add(source.Problem(path, $"'{name}' is neither a tool of the agent nor one of its event types."));
        }
    }

    private static void CheckText(YamlSource source, string path, TextCheck text, List<Problem> problems)
    {
        if (text.Contains is null && text.NotContains is null && text.Pattern is null)
        {
            problems.Add(source.Problem(path, "A text check needs at least one of 'contains', 'notContains', 'pattern'."));
        }
        if (text.Pattern is not null)
        {
            try
            {
                _ = new Regex(text.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException e)
            {
                problems.Add(source.Problem($"{path}.pattern", $"The pattern is not a valid regular expression: {e.Message}"));
            }
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
