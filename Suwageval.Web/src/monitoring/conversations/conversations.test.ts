import { describe, expect, it } from "vitest";

import {
    checksVerdict,
    checksText,
    draftCutOf,
    MESSAGE_PREVIEW_LENGTH,
    messageOptions,
    scenarioDraftPath,
    draftTurns,
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

describe("draftTurns", () => {
    const items = [
        { kind: "UserMessage" },
        { kind: "AssistantMessage" },
        { kind: "UserMessage" },
        { kind: "ToolCall" },
        { kind: "AssistantMessage" },
        { kind: "UserMessage" },
        { kind: "AssistantMessage" },
    ];

    it("counts the turns the real conversation had after the cut, the agent's answer to a cut user message included", () => {
        expect(draftTurns(items, "user", 1)).toBe(3);
        expect(draftTurns(items, "user", 3)).toBe(1);
        expect(draftTurns(items, "agent", 1)).toBe(2);
        expect(draftTurns(items, "agent", 3)).toBe(0);
        expect(draftTurns(items, "agent", 9)).toBe(0);
    });

    it("stops at the most turns a scenario may run", () => {
        expect(
            draftTurns(
                Array.from({ length: 30 }, () => ({ kind: "UserMessage" })),
                "user",
                1,
            ),
        ).toBe(20);
    });
});

describe("scenario draft", () => {
    it("asks for the whole conversation or up to a user or agent message", () => {
        expect(scenarioDraftPath("c1", "end", 1)).toBe("/monitoring/conversations/c1/scenario");
        expect(scenarioDraftPath("c1", "user", 2)).toBe("/monitoring/conversations/c1/scenario?upToUserMessage=2");
        expect(scenarioDraftPath("c1", "agent", 1, 4)).toBe(
            "/monitoring/conversations/c1/scenario?upToAgentMessage=1&simulate=true&turns=4",
        );
        expect(scenarioDraftPath("c1", "agent", 1)).toBe("/monitoring/conversations/c1/scenario?upToAgentMessage=1");
        expect(draftCutOf("agent")).toBe("agent");
        expect(draftCutOf(null)).toBe("end");
    });

    it("numbers the messages of one side and shows the start of each", () => {
        const items = [
            { kind: "UserMessage", text: "Where is\nA-17?" },
            { kind: "AssistantMessage", text: "x".repeat(MESSAGE_PREVIEW_LENGTH + 5) },
            { kind: "ToolCall" },
            { kind: "UserMessage", text: "Thanks" },
        ];

        expect(messageOptions(items, "user")).toEqual([
            { value: "1", label: "1. Where is A-17?" },
            { value: "2", label: "2. Thanks" },
        ]);
        expect(messageOptions(items, "agent")[0]?.label).toBe(`1. ${"x".repeat(MESSAGE_PREVIEW_LENGTH)}…`);
    });
});
