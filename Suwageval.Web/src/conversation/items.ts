import type { components } from "../api/openapi";

export type ConversationItem = components["schemas"]["ConversationItem"];

export function itemKey(item: ConversationItem): string {
    return JSON.stringify(item);
}

export function itemTitle(item: ConversationItem): string {
    switch (item.kind) {
        case "UserMessage":
            return "user";
        case "AssistantMessage":
            return item.interrupted === true ? "agent (interrupted)" : "agent";
        case "ToolCall":
            return `call: ${item.name}`;
        case "ToolResult":
            return item.isError === true ? "tool result (error)" : "tool result";
        case "Retrieval":
            return `retrieval: ${item.query}`;
        case "Event":
            return `event: ${item.type}`;
        case undefined:
            break;
    }
    return "?";
}

export function itemBody(item: ConversationItem): string {
    switch (item.kind) {
        case "UserMessage":
        case "AssistantMessage":
            return item.text;
        case "ToolCall":
            return JSON.stringify(item.arguments, null, 2);
        case "ToolResult":
            return item.content;
        case "Retrieval":
            return item.chunks
                .map((chunk) => `[${chunk.source}${chunk.score == null ? "" : `, ${chunk.score.toFixed(2)}`}] ${chunk.text}`)
                .join("\n\n");
        case "Event":
            return JSON.stringify(item.data, null, 2);
        case undefined:
            break;
    }
    return "";
}
