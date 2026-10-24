// The items of a conversation log as the pages show them: a run's samples and a monitored conversation's turns.
import type { components } from "../api/openapi";

export type ConversationItem = components["schemas"]["ConversationItem"];

/** An item's identity for highlighting: items have no id, so their content is it. */
export function itemKey(item: ConversationItem): string {
    return JSON.stringify(item);
}

/** What an item is, in a word or two: "user", "call: get_order", "event: state.changed". */
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

/** The body of an item as text: message text, pretty arguments, a result, retrieved chunks. */
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
