using System.Text.Json.Serialization;

namespace Suwageval.Contracts.Checks;

/// <summary>Whose messages a <see cref="MessageCountCheck"/> counts, written <c>user</c> or <c>assistant</c>.</summary>
[JsonConverter(typeof(MessageRoleConverter))]
public enum MessageRole
{
    User,
    Assistant,
}
