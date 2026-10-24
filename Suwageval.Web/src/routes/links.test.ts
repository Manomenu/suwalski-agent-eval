import { describe, expect, it } from "vitest";

import {
    agentEvaluationHref,
    agentHref,
    agentVersionsHref,
    compareGroupHref,
    compareHref,
    runHref,
    scenarioInRunHref,
    versionCompareHref,
    versionHref,
    versionInUseHref,
    versionsInEvaluationHref,
    agentVersionsFilteredHref,
} from "./links";

describe("links", () => {
    it("encodes the agent name", () => {
        expect(agentHref("my bot")).toBe("/agents/my%20bot");
        expect(agentEvaluationHref("my bot")).toBe("/agents/my%20bot/evaluation");
        expect(agentVersionsHref("my bot")).toBe("/agents/my%20bot/versions");
        expect(versionHref("my bot", "abc")).toBe("/agents/my%20bot/versions/abc");
        expect(versionCompareHref("my bot", "a", "b")).toBe("/agents/my%20bot/versions/compare?a=a&b=b");
    });

    it("links the Versions list by usage and version", () => {
        expect(versionsInEvaluationHref("my bot")).toBe("/agents/my%20bot/versions?usage=evaluation");
        expect(versionInUseHref("bot", "abc", false)).toBe("/agents/bot/versions?usage=evaluation&version=abc");
        expect(versionInUseHref("bot", "abc", true)).toBe("/agents/bot/versions?usage=evaluation,groups&version=abc");
        expect(agentVersionsFilteredHref("bot", ["unused", "archived"], ["a", "b"])).toBe(
            "/agents/bot/versions?usage=unused,archived&version=a,b",
        );
    });

    it("links a run, and a scenario in it", () => {
        expect(runHref("abc")).toBe("/runs/abc");
        expect(scenarioInRunHref("abc", "refund")).toBe("/runs/abc#scenario-refund");
    });

    it("links the comparison of runs, with a baseline when there is one", () => {
        expect(compareHref(["a", "b"])).toBe("/compare?runs=a,b");
        expect(compareHref(["a", "b"], "a")).toBe("/compare?runs=a,b&baseline=a");
    });

    it("links the comparison of a group of an agent", () => {
        expect(compareGroupHref("sweep-1", "my bot")).toBe("/compare?group=sweep-1&agent=my+bot");
    });
});
