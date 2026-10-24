import { describe, expect, it } from "vitest";

import type { ConversationItem, ScenarioRunResult } from "./api";
import { isSimulated, sampleTitle, simulationTurns } from "./simulation";

const agent = (text: string): ConversationItem => ({ kind: "AssistantMessage", text });
const user = (text: string): ConversationItem => ({ kind: "UserMessage", text });
const call: ConversationItem = { kind: "ToolCall", id: "1", name: "get_order", arguments: {} };

function sample(fields: Partial<ScenarioRunResult["samples"][number]>): ScenarioRunResult["samples"][number] {
    return { sample: 2, status: "Done", ...fields };
}

describe("simulationTurns", () => {
    it("opens a turn at every user message, the agent's first turn before them", () => {
        expect(simulationTurns([agent("a1"), user("u2"), call, agent("a2"), user("u3")])).toEqual([
            [agent("a1")],
            [user("u2"), call, agent("a2")],
            [user("u3")],
        ]);
    });

    it("has no empty turn when the user opened the conversation, and none at all without output", () => {
        expect(simulationTurns([user("u1"), agent("a1")])).toEqual([[user("u1"), agent("a1")]]);
        expect(simulationTurns([])).toEqual([]);
    });
});

describe("sampleTitle", () => {
    it("is the agent's output for a sample of one turn", () => {
        const one = sample({ durationMs: 1200 });
        expect(isSimulated(one)).toBe(false);
        expect(sampleTitle(one)).toBe("Agent output, sample 2 · 1.2 s");
    });

    it("is the conversation with its turns, and says when the simulated user ended it", () => {
        const simulated = sample({ durationMs: 4000, turnDurationsMs: [1000, 1000, 2000], endedByUser: true });
        expect(isSimulated(simulated)).toBe(true);
        expect(sampleTitle(simulated)).toBe("Conversation, sample 2 · 3 turns · ended by the user · 4.0 s");
        expect(sampleTitle(sample({ turnDurationsMs: [] }))).toBe("Conversation, sample 2 · 0 turns");
    });
});
