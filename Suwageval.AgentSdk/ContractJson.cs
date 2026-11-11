using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Suwageval.AgentSdk;

/// <summary>The JSON shape of every contract record: camelCase, strict about what it reads.</summary>
public static class ContractJson
{
    /// <summary>
    /// Unknown fields, missing required ones and nulls in non-nullable members are errors, so a typo in a scenario file
    /// is reported instead of silently ignored, and a number is a JSON number, never a string holding one. A list added to the
    /// contract later (<see cref="Agents.ContextDefinition.Tools"/>) is left out while empty, so a contract that does not use it hashes
    /// as before and an older reader still reads it. Read-only: share it, do not change it.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>The same rules on options someone else owns (ASP.NET Core's), so the server reads requests as strictly as the CLI reads files.</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.AllowOutOfOrderMetadataProperties = true;
        options.PropertyNameCaseInsensitive = false;
        options.NumberHandling = JsonNumberHandling.Strict;
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyLists } },
        };
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static void OmitEmptyLists(JsonTypeInfo type)
    {
        foreach (var property in type.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(OmittedWhenEmptyAttribute), inherit: false) == true)
            {
                property.ShouldSerialize = static (_, value) => value is IReadOnlyCollection<object> { Count: > 0 };
            }
        }
    }
}
