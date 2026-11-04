namespace Suwageval.Server.Problems;

/// <summary>A mistake in a request, named by the field it belongs to (<c>scenarios[2].key</c>).</summary>
internal sealed record FieldError(string Field, string Message);
