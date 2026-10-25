using Suwageval.Cli.Files;
using Suwageval.Cli.Validation;

namespace Suwageval.Cli.Tests.Validation;

/// <summary>The monitoring checks of <c>monitoring-checks.yaml</c>, validated with the rest of the agent's folder.</summary>
public sealed class MonitoringChecksValidationTests
{
    private static ValidationResult Validate(string checks) =>
        Validator.Validate(
            new AgentFolder(
                "agent.yaml",
                TestAgent.Yaml,
                [new SourceText("s.yaml", TestAgent.Scenario)],
                new SourceText(AgentFolder.MonitoringChecksFile, checks)
            ),
            TestAgent.Contract
        );

    private static string Problems(ValidationResult result) => string.Join('\n', TestAgent.Lines(result.Problems));

    [Fact]
    public void Valid_checks_are_read_and_counted()
    {
        var result = Validate(
            """
            checks:
              - key: no-refund
                type: tool-not-called
                tool: refund_order
                description: Never refunds on its own.
            """
        );

        Assert.Empty(result.Problems);
        Assert.Equal(
            "no-refund",
            Assert
                .Single(Assert.IsType<IReadOnlyList<Suwageval.Contracts.Checks.CheckInput>>(result.MonitoringChecks, exactMatch: false))
                .Key
        );
    }

    [Fact]
    public void A_check_without_a_description_is_valid_but_one_with_a_scenarios_options_is_a_problem()
    {
        var problems = Problems(
            Validate(
                """
                checks:
                  - key: no-refund
                    type: tool-not-called
                    tool: refund_order
                    samples: 3
                """
            )
        );

        Assert.DoesNotContain("description", problems, StringComparison.Ordinal);
        Assert.Contains("samples", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tool_the_contract_does_not_have_is_a_problem()
    {
        var problems = Problems(
            Validate(
                """
                checks:
                  - key: no-wire
                    type: tool-not-called
                    tool: wire_money
                    description: Never wires money.
                """
            )
        );

        Assert.Contains("wire_money", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_checks_may_stand_at_the_root()
    {
        var problems = Problems(Validate("checks: []\nscenarios: []\n"));

        Assert.Contains("one key, 'checks'", problems, StringComparison.Ordinal);
    }
}
