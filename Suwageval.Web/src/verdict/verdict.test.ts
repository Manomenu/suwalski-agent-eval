import { describe, expect, it } from "vitest";

import { itemKey } from "../conversation/items";
import { failedEvidence, VERDICT_VARIANT, verdictOf } from "./verdict";

describe("verdictOf", () => {
    it("tells passed, failed and not judged apart", () => {
        expect(verdictOf({ passed: true })).toBe("passed");
        expect(verdictOf({ passed: false })).toBe("failed");
        expect(verdictOf({ passed: false, notJudged: true })).toBe("not judged");
    });
});

describe("VERDICT_VARIANT", () => {
    it("gives every verdict its badge variant", () => {
        expect(VERDICT_VARIANT).toEqual({ passed: "success", failed: "danger", "not judged": "warning" });
    });
});

describe("failedEvidence", () => {
    it("takes the evidence of the checks that did not pass", () => {
        const call = { kind: "ToolCall" as const, id: "c1", name: "refund_order", arguments: {} };
        const answer = { kind: "AssistantMessage" as const, text: "Hi" };

        const keys = failedEvidence([
            { passed: false, evidence: [call] },
            { passed: true, evidence: [answer] },
        ]);

        expect([...keys]).toEqual([itemKey(call)]);
    });
});
