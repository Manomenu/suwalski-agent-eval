using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

[JsonConverter(typeof(MessageRoleConverter))]
public enum MessageRole
{
    User,
    Assistant,
}
