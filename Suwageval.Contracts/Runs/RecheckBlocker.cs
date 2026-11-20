using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<RecheckBlocker>))]
public enum RecheckBlocker
{
    NotFinished,

    AllSkipped,

    Mismatches,

    NothingChanged,

    GateRun,
}
