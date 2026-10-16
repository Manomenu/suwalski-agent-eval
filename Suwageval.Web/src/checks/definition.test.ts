import { describe, expect, it } from "vitest";

import type { CheckDefinition } from "./api";
import { definitionSummary, definitionYaml, revisionNote } from "./definition";

// `where.turn` is typed `unknown` by the API schema; the JSON carries a number, "last" or "any".
const toolCalled: CheckDefinition = {
    passThreshold: 1,
    where: { turn: 1, after: "get_order" },
    args: { order_id: "A-17" },
    tool: "refund_order",
    scenarioKey: "refund",
    type: "tool-called",
    key: "refund-called",
};

describe("definitionYaml", () => {
    it("puts key and type first, the rest as stored, where and passThreshold last", () => {
        expect(definitionYaml(toolCalled)).toBe(
            [
                "key: refund-called",
                "type: tool-called",
                "args:",
                "  order_id: A-17",
                "tool: refund_order",
                "scenarioKey: refund",
                "where:",
                "  turn: 1",
                "  after: get_order",
                "passThreshold: 1",
                "",
            ].join("\n"),
        );
    });
});

describe("definitionSummary", () => {
    it("names the tool, turn, after and threshold", () => {
        expect(definitionSummary(toolCalled)).toBe("tool-called refund_order · turn 1 · after get_order · threshold 100%");
    });

    it("says a judge with its turn, and leaves out what is absent", () => {
        const judge: CheckDefinition = {
            type: "judge",
            key: "polite",
            scenarioKey: "refund",
            criteria: "Be polite.",
            where: { turn: "last" },
        };
        expect(definitionSummary(judge)).toBe("judge · turn last");
    });

    it("leaves out the default turn, and says before", () => {
        const check: CheckDefinition = {
            type: "tool-not-called",
            key: "no-cancel",
            scenarioKey: "refund",
            tool: "cancel_order",
            where: { turn: "any", before: "refund_order" },
            passThreshold: 0.8,
        };
        expect(definitionSummary(check)).toBe("tool-not-called cancel_order · before refund_order · threshold 80%");
    });

    it("names an event and the parts of a text check", () => {
        expect(definitionSummary({ type: "event", key: "e", scenarioKey: "s", event: "state.changed" })).toBe("event state.changed");
        expect(definitionSummary({ type: "text", key: "t", scenarioKey: "s", contains: "refund", pattern: "^Hi" })).toBe(
            'text contains "refund" pattern "^Hi"',
        );
    });
});

describe("revisionNote", () => {
    it("is null while the run saw the latest revision", () => {
        expect(revisionNote(2, 2)).toBeNull();
    });

    it("says the check changed since the run", () => {
        expect(revisionNote(2, 3)).toBe("Changed since this run (rev 2 → 3)");
    });
});
