using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts;

/// <summary>The JSON shape of every contract record: camelCase, strict about what it reads.</summary>
public static class ContractJson
{
    /// <summary>
    /// Unknown fields, missing required ones and nulls in non-nullable members are errors, so a typo in a scenario file
    /// is reported instead of silently ignored. Read-only: share it, do not change it.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            AllowOutOfOrderMetadataProperties = true,
            PropertyNameCaseInsensitive = false,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
