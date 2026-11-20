using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Suwageval.AgentSdk;

public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = Create();

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
