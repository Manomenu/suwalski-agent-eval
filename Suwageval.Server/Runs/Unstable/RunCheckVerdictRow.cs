namespace Suwageval.Server.Runs.Unstable;

/// <summary>One check of one scenario in one finished run, counted over the run's samples: how many had a verdict, how many passed.</summary>
internal sealed record RunCheckVerdictRow(
    Guid RunId,
    DateTime CreatedAt,
    Guid AgentVersionId,
    string ScenarioKey,
    string CheckKey,
    int CheckRevision,
    int Samples,
    int SamplesPassed,
    double PassThreshold
);
