using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<RegressionState>))]
public enum RegressionState
{
    Passed,

    Regression,

    New,

    AlreadyBroken,

    KnownFailure,

    Fixed,

    Unstable,

    AgentError,
}
