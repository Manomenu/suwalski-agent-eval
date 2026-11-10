import { describe, expect, it } from "vitest";

import type { CheckDefinition } from "./api";
import { definitionSummary, definitionYaml, revisionNote } from "./definition";

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
    it("leaves out the scenario's key and an empty where, but keeps a null the check expects", () => {
        const check: CheckDefinition = {
            type: "tool-called",
            key: "k",
            scenarioKey: "s",
            tool: "t",
            args: { note: null },
            where: {},
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

describe("a check's own samples", () => {
    const withSamples: CheckDefinition = { ...toolCalled, samples: 1 };

    it("closes the YAML after passThreshold", () => {
        expect(definitionYaml(withSamples).endsWith("passThreshold: 1\nsamples: 1\n")).toBe(true);
    });

    it("is said in the summary only when the check sets it", () => {
        expect(definitionSummary(withSamples)).toBe("tool-called refund_order · turn 1 · after get_order · threshold 100% · 1 sample");
        expect(definitionSummary({ ...toolCalled, samples: 5 }).endsWith("· 5 samples")).toBe(true);
        expect(definitionSummary(toolCalled)).not.toContain("sample");
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
        expect(definitionSummary({ type: "text", key: "t", scenarioKey: "s", contains: ["refund"], pattern: "^Hi" })).toBe(
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

describe("a message-count check", () => {
    it("AC-message-count-check-6: names the role and the bounds, and the turn as for any check", () => {
        expect(
            definitionSummary({ type: "message-count", key: "m", scenarioKey: "s", role: "assistant", max: 1, where: { turn: "last" } }),
        ).toBe("message-count assistant at most 1 · turn last");
        expect(definitionSummary({ type: "message-count", key: "m", scenarioKey: "s", min: 1, max: 2 })).toBe(
            "message-count at least 1 at most 2",
        );
        expect(definitionSummary({ type: "message-count", key: "m", scenarioKey: "s", role: "user", exactly: 0 })).toBe(
            "message-count user exactly 0",
        );
    });

    it("AC-message-count-check-6: shows its fields in the YAML of the check", () => {
        expect(definitionYaml({ type: "message-count", key: "m", scenarioKey: "s", role: "assistant", max: 1 })).toBe(
            "key: m\ntype: message-count\nrole: assistant\nmax: 1\n",
        );
    });
});

describe("the event and text check options", () => {
    it("AC-check-options-7: an event check shows last and its bounds", () => {
        expect(definitionSummary({ type: "event", key: "e", scenarioKey: "s", event: "procedure.started", max: 1 })).toBe(
            "event procedure.started at most 1",
        );
        expect(definitionSummary({ type: "event", key: "e", scenarioKey: "s", event: "procedure.started", min: 0, max: 2 })).toBe(
            "event procedure.started at least 0 at most 2",
        );
        expect(definitionSummary({ type: "event", key: "e", scenarioKey: "s", event: "state.changed", last: true, data: {} })).toBe(
            "event state.changed last",
        );
        expect(definitionSummary({ type: "event", key: "e", scenarioKey: "s", event: "x", exactly: 2 })).toBe("event x exactly 2");
    });

    it("AC-check-options-7: a text check shows every text, one stored as a plain string too", () => {
        expect(definitionSummary({ type: "text", key: "t", scenarioKey: "s", notContains: ["otherType", "quoteRequest"] })).toBe(
            'text not contains "otherType", "quoteRequest"',
        );
        const stored = { type: "text", key: "t", scenarioKey: "s", contains: "refund" } as unknown as Parameters<
            typeof definitionSummary
        >[0];
        expect(definitionSummary(stored)).toBe('text contains "refund"');
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
