using Microsoft.AspNetCore.Http.HttpResults;
using Suwageval.Contracts.Refusals;
using Suwbackkit.Server;

namespace Suwageval.Server.Problems;

internal static class ProblemResults
{
    public const string FixInAgentFiles =
        "Correct it in the agent's suwageval files (`suwageval validate` names the file and line), then push again.";

    public static RouteHandlerBuilder ProducesRefusal(this RouteHandlerBuilder builder, params int[] statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        foreach (var status in statuses)
        {
            builder.ProducesRefusal<RefusalCode, MistakeCode, Mistake<MistakeCode>>(status);
        }
        return builder;
    }

    public static ProblemHttpResult AgentNotFound(string name) => NotFound(RefusalCode.AgentNotFound, "Agent", name);

    public static ProblemHttpResult VersionNotFound(string id) => NotFound(RefusalCode.VersionNotFound, "Version", id);

    public static ProblemHttpResult ScenarioNotFound(string key) => NotFound(RefusalCode.ScenarioNotFound, "Scenario", key);

    public static ProblemHttpResult CheckNotFound(string key) => NotFound(RefusalCode.CheckNotFound, "Check", key);

    public static ProblemHttpResult RunNotFound(Guid id) => NotFound(RefusalCode.RunNotFound, "Run", id.ToString());

    public static ProblemHttpResult RunGroupNotFound(string name) => NotFound(RefusalCode.RunGroupNotFound, "Run group", name);

    public static ProblemHttpResult ConversationNotFound(Guid id) =>
        NotFound(RefusalCode.ConversationNotFound, "Conversation", id.ToString());

    public static ProblemHttpResult Conflict(RefusalCode code, string title, string detail) =>
        Refusals.Refuse(StatusCodes.Status409Conflict, title, code, detail);

    public static ProblemHttpResult Unfit(RefusalCode code, string title, IEnumerable<string> mismatches) =>
        Refusals.Refuse(
            StatusCodes.Status409Conflict,
            title,
            code,
            [
                .. mismatches.Select(mismatch => new Mistake<MistakeCode>(
                    MistakeCode.VersionMismatch,
                    mismatch,
                    "Make the scenario or check fit the agent's version and push it, or run a version of the agent it fits."
                )),
            ]
        );

    public static ProblemHttpResult Invalid(IEnumerable<FieldError> errors, string fix) =>
        Refusals.Refuse(
            StatusCodes.Status422UnprocessableEntity,
            "The request is not valid",
            RefusalCode.RequestInvalid,
            [.. errors.Select(error => new Mistake<MistakeCode>(MistakeCode.FieldInvalid, error.Message, fix) { At = error.Field })]
        );

    public static ProblemHttpResult Invalid(string field, string message, string fix) => Invalid([new FieldError(field, message)], fix);

    public static ProblemHttpResult EvaluationOff(string agent) => FeatureOff(RefusalCode.EvaluationOff, agent, "evaluation");

    public static ProblemHttpResult MonitoringOff(string agent) => FeatureOff(RefusalCode.MonitoringOff, agent, "monitoring");

    public static IResult BadGateway(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status502BadGateway);

    private static ProblemHttpResult NotFound(RefusalCode code, string what, string name) =>
        Refusals.Refuse(StatusCodes.Status404NotFound, $"{what} not found", code, $"There is no {what.ToLowerInvariant()} '{name}'.");

    private static ProblemHttpResult FeatureOff(RefusalCode code, string agent, string feature) =>
        Conflict(
            code,
            "Feature off",
            $"{char.ToUpperInvariant(feature[0])}{feature[1..]} is off for agent '{agent}': switch it on with features.{feature} in its suwageval/agent.yaml and push."
        );
}
