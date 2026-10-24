import { describe, expect, it } from "vitest";

import { agentDeletionSummary, confirmsDeletion, deletionSummary } from "./deletion";

describe("confirmsDeletion", () => {
    it("wants the id exactly", () => {
        expect(confirmsDeletion("abc-1", "abc-1")).toBe(true);
        expect(confirmsDeletion("", "abc-1")).toBe(false);
        expect(confirmsDeletion("abc-", "abc-1")).toBe(false);
        expect(confirmsDeletion("ABC-1", "abc-1")).toBe(false);
        expect(confirmsDeletion(" abc-1", "abc-1")).toBe(false);
    });
});

describe("deletionSummary", () => {
    it("counts what goes", () => {
        expect(deletionSummary({ runs: 3, samples: 12, verdicts: 36 })).toBe("3 runs, 12 samples and 36 verdicts will be deleted");
        expect(deletionSummary({ runs: 1, samples: 1, verdicts: 1 })).toBe("1 run, 1 sample and 1 verdict will be deleted");
        expect(deletionSummary({ runs: 1, samples: 1, verdicts: 1, conversations: 0 })).toBe(
            "1 run, 1 sample and 1 verdict will be deleted",
        );
        expect(deletionSummary({ runs: 0, samples: 0, verdicts: 0, conversations: 2 })).toBe(
            "0 runs, 0 samples, 0 verdicts and 2 monitored conversations will be deleted",
        );
    });
});

describe("agentDeletionSummary", () => {
    it("counts what goes with the agent", () => {
        expect(agentDeletionSummary({ versions: 2, scenarios: 5, checks: 1, runs: 3, samples: 12, verdicts: 36 })).toBe(
            "2 versions, 5 scenarios, 1 check, 3 runs, 12 samples and 36 verdicts will be deleted",
        );
    });
});
