using Suwageval.AgentSdk.Conversations;
using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Scenarios;

/// <summary>The shared rules of a scenario's <c>continuation</c> (<c>Suwageval.Contracts/Scenarios</c>), tested here because Contracts has no test project.</summary>
public sealed class ContinuationRulesTests
{
    private static ScenarioInput Scenario(int turns = 1, string? simulatedUser = null, params ConversationItem[] conversation) =>
        new()
        {
            Key = "s",
            Conversation = conversation.Length == 0 ? [new UserMessage { Text = "hi" }] : conversation,
            Continuation = new Continuation { Turns = turns, SimulatedUser = simulatedUser },
        };

    private static TextCheck Check(TurnSelector turn) =>
        new()
        {
            Key = "c",
            ScenarioKey = "s",
            Contains = "x",
            Where = new CheckWhere { Turn = turn },
        };

    [Theory]
    [InlineData(1, null)]
    [InlineData(1, "Anna, annoyed")]
    [InlineData(20, "Anna, annoyed")]
    public void One_turn_or_a_simulation_of_up_to_twenty_is_fine(int turns, string? simulatedUser) =>
        Assert.Empty(ContinuationRules.Problems(Scenario(turns, simulatedUser)));

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Turns_are_from_one_to_twenty(int turns) =>
        Assert.Contains(("continuation.turns", "turns is from 1 to 20."), ContinuationRules.Problems(Scenario(turns, "Anna")));

    [Fact]
    public void More_than_one_turn_needs_a_simulated_user()
    {
        var (path, message) = Assert.Single(ContinuationRules.Problems(Scenario(2)));

        Assert.Equal("continuation.turns", path);
        Assert.Contains("needs continuation.simulatedUser", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_simulated_user_is_a_problem() =>
        Assert.Equal("continuation.simulatedUser", Assert.Single(ContinuationRules.Problems(Scenario(3, "  "))).Path);

    [Fact]
    public void A_simulation_may_start_after_an_agent_message() =>
        Assert.Empty(ContinuationRules.Problems(Scenario(2, "Anna", new AssistantMessage { Text = "Hello! How can I help?" })));

    [Fact]
    public void A_check_of_a_simulation_never_picks_a_turn_by_number()
    {
        var simulation = new Continuation { Turns = 3, SimulatedUser = "Anna" };

        Assert.Contains("'any' or 'last'", ContinuationRules.CheckProblem(Check(new TurnSelector(TurnSelectorKind.Number, 2)), simulation));
        Assert.Null(ContinuationRules.CheckProblem(Check(new TurnSelector(TurnSelectorKind.Last)), simulation));
        Assert.Null(ContinuationRules.CheckProblem(Check(default), simulation));
        Assert.Null(ContinuationRules.CheckProblem(Check(new TurnSelector(TurnSelectorKind.Number, 1)), new Continuation()));
    }
}
