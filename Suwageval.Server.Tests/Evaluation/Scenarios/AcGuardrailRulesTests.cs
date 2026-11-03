using Suwageval.Contracts.Agents;
using Suwageval.Contracts.Scenarios;

namespace Suwageval.Server.Tests.Evaluation.Scenarios;

/// <summary>
/// The shared rules of <c>acGuardrail</c> and <c>acGuardrailRetryLimit</c> (<c>Suwageval.Contracts/Scenarios/AcGuardrailRules.cs</c>), tested
/// here because Contracts has no test project of its own; <c>suwageval validate</c> and the server both apply them.
/// </summary>
public sealed class AcGuardrailRulesTests
{
    [Theory]
    [Trait("criterion", "AC-gate-runs-1")]
    [InlineData("AC-refunds-1")]
    [InlineData("AC-gate-runs-12")]
    [InlineData("AC-x2-30")]
    public void An_id_of_the_form_ac_feature_number_is_fine(string id) =>
        Assert.Empty(AcGuardrailRules.Problems(new AcGuardrail { Id = id, RetryLimit = 2 }));

    [Theory]
    [Trait("criterion", "AC-gate-runs-1")]
    [InlineData("refunds-1")]
    [InlineData("ac-refunds-1")]
    [InlineData("AC-refunds")]
    [InlineData("AC-Refunds-1")]
    [InlineData("AC-refunds-0")]
    [InlineData("AC-refunds-01")]
    [InlineData("AC--1")]
    [InlineData("")]
    public void Any_other_id_is_a_problem_at_its_path(string id) =>
        Assert.Equal(["acGuardrail.id"], AcGuardrailRules.Problems(new AcGuardrail { Id = id }).Select(problem => problem.Path));

    [Theory]
    [Trait("criterion", "AC-gate-runs-1")]
    [InlineData(0)]
    [InlineData(21)]
    public void A_retry_limit_outside_one_to_twenty_is_a_problem(int retryLimit) =>
        Assert.Equal(
            [("acGuardrail.retryLimit", "acGuardrail.retryLimit is from 1 to 20 (the default is 5).")],
            AcGuardrailRules.Problems(new AcGuardrail { Id = "AC-refunds-1", RetryLimit = retryLimit })
        );

    [Theory]
    [Trait("criterion", "AC-gate-runs-6")]
    [InlineData(null, null, 5)]
    [InlineData(null, 3, 3)]
    [InlineData(2, 3, 2)]
    [InlineData(4, null, 4)]
    public void The_scenarios_limit_wins_then_the_agents_then_five(int? scenario, int? agent, int expected) =>
        Assert.Equal(expected, AcGuardrailRules.EffectiveRetryLimit(scenario, agent));

    [Fact]
    [Trait("criterion", "AC-gate-runs-1")]
    public void The_agents_default_limit_is_checked_too() =>
        Assert.Equal(
            ["acGuardrailRetryLimit"],
            AgentRules
                .Problems(
                    new AgentDefinition
                    {
                        Name = "a",
                        EndpointUrl = "http://a.test",
                        AcGuardrailRetryLimit = 0,
                    }
                )
                .Select(problem => problem.Path)
        );
}
