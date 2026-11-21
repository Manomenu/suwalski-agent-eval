using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Refusals;

[JsonConverter(typeof(JsonStringEnumConverter<RefusalCode>))]
public enum RefusalCode
{
    [JsonStringEnumMemberName("request.invalid")]
    RequestInvalid,

    [JsonStringEnumMemberName("agent.notFound")]
    AgentNotFound,

    [JsonStringEnumMemberName("version.notFound")]
    VersionNotFound,

    [JsonStringEnumMemberName("scenario.notFound")]
    ScenarioNotFound,

    [JsonStringEnumMemberName("check.notFound")]
    CheckNotFound,

    [JsonStringEnumMemberName("run.notFound")]
    RunNotFound,

    [JsonStringEnumMemberName("runGroup.notFound")]
    RunGroupNotFound,

    [JsonStringEnumMemberName("conversation.notFound")]
    ConversationNotFound,

    [JsonStringEnumMemberName("evaluation.off")]
    EvaluationOff,

    [JsonStringEnumMemberName("monitoring.off")]
    MonitoringOff,

    [JsonStringEnumMemberName("environment.notTaken")]
    EnvironmentNotTaken,

    [JsonStringEnumMemberName("runs.active")]
    RunsActive,

    [JsonStringEnumMemberName("version.notArchived")]
    VersionNotArchived,

    [JsonStringEnumMemberName("runGroup.notArchived")]
    RunGroupNotArchived,

    [JsonStringEnumMemberName("version.unfit")]
    VersionUnfit,

    [JsonStringEnumMemberName("judge.notConfigured")]
    JudgeNotConfigured,

    [JsonStringEnumMemberName("simulator.notConfigured")]
    SimulatorNotConfigured,

    [JsonStringEnumMemberName("recheck.impossible")]
    RecheckImpossible,
}
