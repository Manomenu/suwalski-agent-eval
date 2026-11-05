using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>Reads and writes a <see cref="MessageRole"/> by its lowercase name, as a conversation's <c>user</c> and <c>assistant</c> shorthands are written.</summary>
public sealed class MessageRoleConverter : JsonStringEnumConverter<MessageRole>
{
    public MessageRoleConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}
