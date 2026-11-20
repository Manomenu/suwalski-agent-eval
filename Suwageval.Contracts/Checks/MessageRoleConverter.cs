using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

public sealed class MessageRoleConverter : JsonStringEnumConverter<MessageRole>
{
    public MessageRoleConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}
