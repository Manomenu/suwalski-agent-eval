namespace Suwageval.Server.Problems;

/// <summary>A mistake in a request, named by the field it belongs to (<c>scenarios[2].key</c>).</summary>
internal sealed record FieldError(string Field, string Message);

/// <summary>The error answers of every endpoint, as Problem Details (RFC 9457).</summary>
internal static class ProblemResults
{
    public static IResult NotFound(string what, string name) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"{what} not found",
            detail: $"There is no {what.ToLowerInvariant()} '{name}'."
        );

    /// <summary>409: the request is fine but the state of the thing does not allow it.</summary>
    public static IResult Conflict(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);

    /// <summary>422 with <c>errors</c>: field name to its messages.</summary>
    public static IResult Invalid(IEnumerable<FieldError> errors) =>
        Results.ValidationProblem(
            errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()),
            statusCode: StatusCodes.Status422UnprocessableEntity
        );

    /// <summary>422 for one field.</summary>
    public static IResult Invalid(string field, string message) => Invalid([new FieldError(field, message)]);

    public static IResult BadGateway(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status502BadGateway);
}
