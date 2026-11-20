using System.Text.Json;

namespace Suwageval.Contracts.Checks;

public sealed record EventNotEmittedCheck : CheckInput
{
    public required string Event { get; init; }

    public JsonElement? Data { get; init; }
}
