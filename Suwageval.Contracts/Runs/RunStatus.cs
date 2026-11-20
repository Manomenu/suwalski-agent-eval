using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Runs;

[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus
{
    Queued,
    Running,
    Done,
    Failed,
    Cancelled,
    Interrupted,
}
