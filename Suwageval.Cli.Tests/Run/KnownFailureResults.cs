using Suwageval.Contracts.Checks;
using Suwageval.Contracts.Runs;

namespace Suwageval.Cli.Tests.Run;

/// <summary>Results of a finished run whose one failing check is a known failure: not a regression, so only <c>--strict</c> fails the run.</summary>
internal static class KnownFailureResults
{
    public static RunResults Create(RunSummary run) =>
        new()
        {
            Run = run,
            Scenarios =
            [
                new ScenarioRunResult
                {
                    Key = "refund",
                    Tags = [],
                    Revision = 1,
                    DefinitionYaml = "",
                    Samples = [],
                    Checks =
                    [
                        new CheckRunResult
                        {
                            Key = "old",
                            Revision = 1,
                            LatestRevision = 1,
                            JudgedByModel = false,
                            Definition = new TextCheck
                            {
                                Key = "old",
                                ScenarioKey = "refund",
                                Contains = "x",
                            },
                            Passed = false,
                            PassRate = 0,
                            PassThreshold = 1,
                            Samples =
                            [
                                new CheckSampleResult
                                {
                                    Sample = 1,
                                    Passed = false,
                                    NotJudged = false,
                                    Evidence = [],
                                    Message = "still wrong",
                                },
                            ],
                            Regression = new CheckRegression { State = RegressionState.KnownFailure },
                        },
                    ],
                },
            ],
            Regression = new RegressionSummary
            {
                ComparedWith = "master",
                History = 5,
                Regressions = 0,
                New = 0,
                AlreadyBroken = 0,
                KnownFailures = 1,
                Fixed = 0,
                Unstable = 0,
                AgentErrors = 0,
            },
        };
}
