using Suwageval.AgentSdk.Conversations;

namespace Suwageval.AgentSdk.Invocation;

public sealed record ToolExchange(ToolCall Call, ToolResult Result);
