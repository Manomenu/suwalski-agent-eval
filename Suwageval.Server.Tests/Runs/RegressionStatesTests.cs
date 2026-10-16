using Suwageval.Contracts.Runs;
using Suwageval.Server.Runs;

namespace Suwageval.Server.Tests.Runs;

public sealed class RegressionStatesTests
{
    private const bool P = true;
    private const bool F = false;

    public static TheoryData<bool[], int, bool, RegressionState> Cases =>
        new()
        {
            // StablePass: the last N all passed
            { [P, P, P, P, P], 5, P, RegressionState.Passed },
            { [P, P, P, P, P], 5, F, RegressionState.Regression },
            { [F, F, P, P, P, P, P], 5, F, RegressionState.Regression },
            { [P], 1, P, RegressionState.Passed },
            { [P], 1, F, RegressionState.Regression },
            // Known: N failures in a row at the end
            { [F, F, F, F, F], 5, F, RegressionState.KnownFailure },
            { [F, F, F, F, F], 5, P, RegressionState.Fixed },
            { [P, F, F, F, F, F], 5, F, RegressionState.KnownFailure },
            { [F], 1, F, RegressionState.KnownFailure },
            { [F], 1, P, RegressionState.Fixed },
            { [P, F], 1, F, RegressionState.KnownFailure },
            // Broken: fewer than N failures after N passes
            { [P, P, P, P, P, F], 5, F, RegressionState.AlreadyBroken },
            { [P, P, P, P, P, F], 5, P, RegressionState.Fixed },
            { [P, P, P, P, P, F, F, F, F], 5, F, RegressionState.AlreadyBroken },
            { [P, P, F], 2, F, RegressionState.AlreadyBroken },
            // NoHistory: fewer than N runs
            { [], 5, P, RegressionState.Passed },
            { [], 5, F, RegressionState.New },
            { [P, P, P, P], 5, P, RegressionState.Passed },
            { [P, P, P, P], 5, F, RegressionState.New },
            { [F], 5, F, RegressionState.New },
            { [F, F, F, F], 5, P, RegressionState.Passed },
            { [], 1, F, RegressionState.New },
            // Mixed: enough history, no steady past
            { [P, F, P, F, P], 5, P, RegressionState.Unstable },
            { [P, F, P, F, P], 5, F, RegressionState.Unstable },
            { [P, P, P, F, F, F, F, P], 5, P, RegressionState.Unstable },
            { [F, P, P, P, F], 5, F, RegressionState.Unstable },
            { [P, F, F], 3, F, RegressionState.Unstable },
            { [P, F, P], 2, F, RegressionState.Unstable },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_state_follows_the_history_and_the_verdict(bool[] history, int n, bool passed, RegressionState expected) =>
        Assert.Equal(expected, RegressionStates.State(history, n, passed));

    private static CheckHistoryPoint[] Runs(params bool[] history) =>
        [
            .. history.Select(
                (passed, index) =>
                    new CheckHistoryPoint
                    {
                        RunId = Guid.NewGuid(),
                        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index),
                        Passed = passed,
                    }
            ),
        ];

    [Fact]
    public void An_already_broken_check_names_the_first_run_of_its_failures_and_every_state_the_last_run_that_passed()
    {
        var history = Runs(P, P, P, P, P, F, F);

        var broken = RegressionStates.Of(history, 5, passed: false, onlyAgentErrors: false);

        Assert.Equal(RegressionState.AlreadyBroken, broken.State);
        Assert.Equal(history[5].RunId, broken.FailingSince);
        Assert.Equal(history[4].RunId, broken.LastPassedRun);
    }

    [Fact]
    public void Only_an_already_broken_check_has_a_failing_since_and_a_check_that_never_passed_has_no_last_passed_run()
    {
        var regression = RegressionStates.Of(Runs(P, P, P, P, P), 5, passed: false, onlyAgentErrors: false);
        var known = RegressionStates.Of(Runs(F, F), 2, passed: false, onlyAgentErrors: false);

        Assert.Equal((RegressionState.Regression, null), (regression.State, regression.FailingSince));
        Assert.Equal((RegressionState.KnownFailure, null, null), (known.State, known.FailingSince, known.LastPassedRun));
    }

    [Fact]
    public void The_history_is_returned_in_order_exactly_as_the_state_was_computed_from()
    {
        var history = Runs(P, P, F, P, F);

        var regression = RegressionStates.Of(history, 5, passed: false, onlyAgentErrors: false);

        Assert.Equal(history, regression.History);
        Assert.Equal([P, P, F, P, F], regression.History.Select(point => point.Passed));
        Assert.Equal(regression.History.OrderBy(point => point.CreatedAt), regression.History);
    }

    [Fact]
    public void A_failure_caused_only_by_agent_errors_is_an_agent_error_whatever_the_history_and_a_pass_stays_passed()
    {
        Assert.Equal(RegressionState.AgentError, RegressionStates.Of(Runs(P, P, P, P, P), 5, passed: false, onlyAgentErrors: true).State);
        Assert.Equal(RegressionState.AgentError, RegressionStates.Of(Runs(F, F), 2, passed: false, onlyAgentErrors: true).State);
        Assert.Equal(RegressionState.Passed, RegressionStates.Of(Runs(P, P, P, P, P), 5, passed: true, onlyAgentErrors: true).State);
    }
}
