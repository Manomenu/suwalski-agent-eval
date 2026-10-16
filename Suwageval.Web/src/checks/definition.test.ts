import { describe, expect, it } from "vitest";

import type { CheckDefinition } from "./api";
import { definitionSummary, definitionYaml, hangingIndent, judgedByModel, revisionNote } from "./definition";

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
    it("leaves out the scenario's key and what is unset, but keeps a null the check expects", () => {
        const check: CheckDefinition = {
            type: "tool-called",
            key: "k",
            scenarioKey: "s",
            tool: "t",
            args: { note: null },
            where: { turn: null, after: null, before: null },
        };
        expect(definitionYaml(check)).toBe(["key: k", "type: tool-called", "tool: t", "args:", "  note: null", ""].join("\n"));
    });

    it("puts key and type first, the rest as stored, where and passThreshold last", () => {
        expect(definitionYaml(toolCalled)).toBe(
            [
                "key: refund-called",
                "type: tool-called",
                "args:",
                "  order_id: A-17",
                "tool: refund_order",
                "where:",
                "  turn: 1",
                "  after: get_order",
                "passThreshold: 1",
                "",
            ].join("\n"),
        );
    });
});

it("keeps long text on one line, for the drawer to wrap", () => {
    const criteria = "The answer tells the customer that the order has already been refunded and offers nothing else at all.";
    const yaml = definitionYaml({ type: "judge", key: "j", scenarioKey: "s", criteria });
    expect(yaml).toContain(`criteria: ${criteria}\n`);
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

    it("names the agent of a call, an event that must not be emitted and the cited sources", () => {
        expect(definitionSummary({ type: "tool-called", key: "c", scenarioKey: "s", tool: "lookup", by: "router" })).toBe(
            "tool-called lookup by router",
        );
        expect(definitionSummary({ type: "event-not-emitted", key: "e", scenarioKey: "s", event: "handoff" })).toBe(
            "event-not-emitted handoff",
        );
        expect(definitionSummary({ type: "cited", key: "c", scenarioKey: "s", sources: ["faq-1", "faq-2"] })).toBe("cited faq-1, faq-2");
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

describe("hangingIndent", () => {
    it("hangs under the value: indentation, list dashes and the key", () => {
        expect(hangingIndent("criteria: The answer tells")).toBe("criteria: ".length);
        expect(hangingIndent("    description: Says the item")).toBe("    description: ".length);
        expect(hangingIndent("  - kettle arrived broken")).toBe("  - ".length);
        expect(hangingIndent("- key: x")).toBe("- key: ".length);
        expect(hangingIndent('  "a:b": value')).toBe('  "a:b": '.length);
    });

    it("hangs only by the indentation when the line has no key", () => {
        expect(hangingIndent("  plain text: no, a key")).toBe("  plain text: ".length);
        expect(hangingIndent("    just text")).toBe(4);
        expect(hangingIndent("pattern: (?i)^a:b$")).toBe("pattern: ".length);
    });
});

describe("judgedByModel", () => {
    it("is true for a judge check and for $judge at any depth of args, result or data", () => {
        expect(judgedByModel({ type: "judge", key: "j", scenarioKey: "s", criteria: "polite" })).toBe(true);
        expect(
            judgedByModel({
                type: "tool-called",
                key: "c",
                scenarioKey: "s",
                tool: "t",
                args: { a: [{ b: { $judge: { description: "x" } } }] },
            }),
        ).toBe(true);
        expect(
            judgedByModel({ type: "tool-called", key: "c", scenarioKey: "s", tool: "t", result: { r: { $judge: { description: "x" } } } }),
        ).toBe(true);
        expect(judgedByModel({ type: "event", key: "e", scenarioKey: "s", event: "e", data: { $judge: { description: "x" } } })).toBe(true);
    });

    it("is false for rules only, $pattern included", () => {
        expect(judgedByModel({ type: "tool-called", key: "c", scenarioKey: "s", tool: "t", args: { a: { $pattern: "^x$" } } })).toBe(false);
        expect(judgedByModel({ type: "text", key: "t", scenarioKey: "s", contains: "$judge" })).toBe(false);
    });
});
