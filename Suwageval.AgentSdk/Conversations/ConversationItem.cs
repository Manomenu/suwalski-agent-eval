using System.Text.Json.Serialization;

namespace Suwageval.AgentSdk.Conversations;

/// <summary>One entry of the conversation log, told apart by <c>kind</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UserMessage), "UserMessage")]
[JsonDerivedType(typeof(AssistantMessage), "AssistantMessage")]
[JsonDerivedType(typeof(ToolCall), "ToolCall")]
[JsonDerivedType(typeof(ToolResult), "ToolResult")]
[JsonDerivedType(typeof(Retrieval), "Retrieval")]
[JsonDerivedType(typeof(Event), "Event")]
public abstract record ConversationItem;
