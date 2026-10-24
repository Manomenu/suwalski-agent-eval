import { describe, expect, it } from "vitest";

import { itemBody, itemTitle, type ConversationItem } from "./items";

const call: ConversationItem = { kind: "ToolCall", id: "1", name: "search", arguments: { q: "x" } };
const reply: ConversationItem = { kind: "AssistantMessage", text: "hi" };

describe("items", () => {
    it("titles and bodies items", () => {
        expect(itemTitle(call)).toBe("call: search");
        expect(itemBody(call)).toBe('{\n  "q": "x"\n}');
        expect(itemBody(reply)).toBe("hi");
        expect(
            itemBody({
                kind: "Retrieval",
                query: "q",
                chunks: [
                    { text: "a", source: "s1", score: 0.5 },
                    { text: "b", source: "s2" },
                ],
            }),
        ).toBe("[s1, 0.50] a\n\n[s2] b");
        expect(itemTitle({ kind: "ToolResult", callId: "1", content: "", isError: true })).toBe("tool result (error)");
    });
});
