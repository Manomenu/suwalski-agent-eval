import { describe, expect, it } from "vitest";

import { agentDeletionSummary, confirmsDeletion, deletionSummary, versionToDelete } from "./deletion";

const active = { id: "a", archivedAt: null };
const archived = { id: "b", archivedAt: "2026-10-01T10:00:00Z" };
const otherArchived = { id: "c", archivedAt: "2026-10-02T10:00:00Z" };

describe("versionToDelete", () => {
    it("is the one selected archived version", () => {
        expect(versionToDelete([active, archived], new Set(["b"]))).toBe(archived);
    });

    it("is nothing for no selection, an active version or several", () => {
        expect(versionToDelete([active, archived], new Set())).toBeNull();
        expect(versionToDelete([active, archived], new Set(["a"]))).toBeNull();
        expect(versionToDelete([active, archived, otherArchived], new Set(["b", "c"]))).toBeNull();
        expect(versionToDelete([active, archived], new Set(["a", "b"]))).toBeNull();
    });
});

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
    });
});

describe("agentDeletionSummary", () => {
    it("counts what goes with the agent", () => {
        expect(agentDeletionSummary({ versions: 2, scenarios: 5, checks: 1, runs: 3, samples: 12, verdicts: 36 })).toBe(
            "2 versions, 5 scenarios, 1 check, 3 runs, 12 samples and 36 verdicts will be deleted",
        );
    });
});
