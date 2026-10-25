import { describe, expect, it } from "vitest";

import {
    checksVerdict,
    checksText,
    replayOptions,
    scenarioDraftPath,
    untilTurnOf,
    conversationsPath,
    initialFilter,
    stateOf,
    versionText,
} from "./conversations";

describe("conversationsPath", () => {
    it("asks for one page, narrowed by the filter", () => {
        expect(conversationsPath("my bot", { environment: null, state: null, failedCheck: null }, 1, 20)).toBe(
            "/monitoring/agents/my%20bot/conversations?page=1&pageSize=20",
        );
        expect(conversationsPath("a", { environment: "production", state: "Closed", failedCheck: "no-refund" }, 3, 20)).toBe(
            "/monitoring/agents/a/conversations?page=3&pageSize=20&environment=production&state=Closed&failedCheck=no-refund",
        );
    });
});

describe("stateOf", () => {
    it("reads the field's value back, anything else being no filter", () => {
        expect(stateOf("Open")).toBe("Open");
        expect(stateOf("Closed")).toBe("Closed");
        expect(stateOf(null)).toBeNull();
        expect(stateOf("open")).toBeNull();
    });
});

describe("versionText", () => {
    it("names the version by its label or the start of its id, and marks a change during the conversation", () => {
        expect(versionText({ agentVersionId: "12345678-aaaa", versionLabel: "v7" })).toBe("v7");
        expect(versionText({ agentVersionId: "12345678-aaaa", versionLabel: null, versionChanged: true })).toBe(
            "12345678 (changed during it)",
        );
    });
});

describe("initialFilter", () => {
    it("takes the failed check from the address, nothing else", () => {
        expect(initialFilter("?failedCheck=no-refund&state=Open")).toEqual({ environment: null, state: null, failedCheck: "no-refund" });
        expect(initialFilter("?failedCheck=%20")).toEqual({ environment: null, state: null, failedCheck: null });
    });
});

describe("checksText", () => {
    it("says not checked, passed, or how many failed", () => {
        expect(checksText({ checkedAt: null })).toBe("Not checked");
        expect(checksText({ checkedAt: "2026-10-24T12:00:00Z", failedChecks: 0 })).toBe("Passed");
        expect(checksText({ checkedAt: "2026-10-24T12:00:00Z", failedChecks: 2 })).toBe("2 failed");
        expect(checksText({ checkedAt: "2026-10-24T12:00:00Z", failedChecks: 0, notJudgedChecks: 1 })).toBe("1 not judged");
        expect(checksText({ checkedAt: "2026-10-24T12:00:00Z", failedChecks: 2, notJudgedChecks: 1 })).toBe("2 failed, 1 not judged");
    });
});

describe("checksVerdict", () => {
    it("is failed on a failure, not judged on checks not judged, passed when all passed, none before checking", () => {
        expect(checksVerdict({ checkedAt: null })).toBeNull();
        expect(checksVerdict({ checkedAt: "x", failedChecks: 1, notJudgedChecks: 1 })).toBe("failed");
        expect(checksVerdict({ checkedAt: "x", notJudgedChecks: 1 })).toBe("not judged");
        expect(checksVerdict({ checkedAt: "x" })).toBe("passed");
    });
});

describe("scenario draft", () => {
    it("asks for the draft as it is or until a turn", () => {
        expect(scenarioDraftPath("c1", null)).toBe("/monitoring/conversations/c1/scenario");
        expect(scenarioDraftPath("c1", 2)).toBe("/monitoring/conversations/c1/scenario?untilTurn=2");
    });

    it("offers as it is and every turn before the last, and reads the choice back", () => {
        expect(replayOptions(3).map((option) => option.value)).toEqual(["", "1", "2"]);
        expect(replayOptions(1)).toHaveLength(1);
        expect(untilTurnOf("2")).toBe(2);
        expect(untilTurnOf("")).toBeNull();
        expect(untilTurnOf(null)).toBeNull();
    });
});
