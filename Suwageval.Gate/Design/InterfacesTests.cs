namespace Suwageval.Gate.Design;

public sealed class InterfacesTests
{
    // Why: the agent under test implements these, outside this repository
    private static readonly string[] ImplementedByAgents =
    [
        "Suwageval.AgentSdk.Invocation.IAgentContractSource",
        "Suwageval.AgentSdk.Invocation.ITurnHandler",
    ];

    [Fact]
    public void Every_interface_has_at_least_two_implementations()
    {
        var lonely = SolutionTypes
            .All.Where(type => type.IsInterface && !ImplementedByAgents.Contains(type.FullName))
            .Select(contract =>
                (contract, implementations: SolutionTypes.All.Count(type => !type.IsInterface && contract.IsAssignableFrom(type)))
            )
            .Where(item => item.implementations < 2)
            .Select(item => $"{item.contract.FullName} ({item.implementations})")
            .Order()
            .ToList();

        Assert.True(
            lonely.Count == 0,
            $"delete the interface and use the class; extract one when the second implementation arrives: {string.Join(", ", lonely)}"
        );
    }
}
