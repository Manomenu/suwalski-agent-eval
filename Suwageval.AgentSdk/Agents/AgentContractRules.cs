namespace Suwageval.AgentSdk.Agents;

/// <summary>
/// What is wrong with an <see cref="AgentContract"/>, written once for the server (which refuses to register such a contract)
/// and for an agent's own unit test.
/// </summary>
public static class AgentContractRules
{
    /// <summary>One sentence for each problem; empty when the contract can be registered.</summary>
    public static IReadOnlyList<string> Problems(AgentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var problems = new List<string>();
        if (contract.Agents.Count == 0)
        {
            problems.Add("agents: there is at least one agent");
        }
        var names = new HashSet<string>();
        for (var i = 0; i < contract.Agents.Count; i++)
        {
            var agent = contract.Agents[i];
            if (string.IsNullOrWhiteSpace(agent.Name) || !names.Add(agent.Name))
            {
                problems.Add($"agents[{i}].name: an agent has a name, and its name is unique");
            }
            if (string.IsNullOrWhiteSpace(agent.Model.Name))
            {
                problems.Add($"agents[{i}].model.name is empty");
            }
            var tools = new HashSet<string>();
            for (var t = 0; t < agent.Tools.Count; t++)
            {
                if (string.IsNullOrWhiteSpace(agent.Tools[t].Name) || !tools.Add(agent.Tools[t].Name))
                {
                    problems.Add($"agents[{i}].tools[{t}].name: a tool has a name, and its name is unique within its agent");
                }
            }
        }
        if (contract.Context.UsesRetrieval && string.IsNullOrWhiteSpace(contract.Context.Retrieval?.DataVersion))
        {
            problems.Add(
                "context.retrieval.dataVersion: an agent that uses retrieval declares the version of its data, "
                    + "or two runs over different data would look like the same version"
            );
        }
        if (!contract.Context.UsesRetrieval && contract.Context.Retrieval is not null)
        {
            problems.Add(
                "context.retrieval: the agent does not use retrieval (usesRetrieval is false), so it has no data version to declare"
            );
        }
        var events = new HashSet<string>();
        for (var i = 0; i < contract.Context.Events.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(contract.Context.Events[i].Type) || !events.Add(contract.Context.Events[i].Type))
            {
                problems.Add($"context.events[{i}].type: an event has a type, and its type is unique");
            }
        }
        return problems;
    }
}
