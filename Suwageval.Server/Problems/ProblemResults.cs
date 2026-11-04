namespace Suwageval.Server.Problems;

internal static class ProblemResults
{
    public static IResult NotFound(string what, string name) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"{what} not found",
            detail: $"There is no {what.ToLowerInvariant()} '{name}'."
        );

    public static IResult Conflict(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);

    public static IResult Invalid(IEnumerable<FieldError> errors) =>
        Results.ValidationProblem(
            errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
            statusCode: StatusCodes.Status422UnprocessableEntity
        );

    public static IResult Invalid(string field, string message) => Invalid([new FieldError(field, message)]);

    public static IResult FeatureOff(string agent, string feature) =>
        Results.Problem(
            title: "Feature off",
            detail: $"{char.ToUpperInvariant(feature[0])}{feature[1..]} is off for agent '{agent}' (features.{feature} in its suwageval/agent.yaml).",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["feature"] = feature }
        );

    public static IResult BadGateway(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status502BadGateway);
}
