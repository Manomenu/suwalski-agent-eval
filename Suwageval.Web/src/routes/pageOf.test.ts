import { describe, expect, it } from "vitest";

import { pageOf } from "./pageOf";

const ID = "3f2b8c1e-5d4a-4e6f-9a7b-0c1d2e3f4a5b";

describe("pageOf", () => {
    it("opens the agent list at the root", () => {
        expect(pageOf("/")).toEqual({ kind: "agents" });
    });

    it("reads the guid of /runs/<guid>", () => {
        expect(pageOf(`/runs/${ID}`)).toEqual({ kind: "run", runId: ID });
        expect(pageOf(`/runs/${ID}/`)).toEqual({ kind: "run", runId: ID });
    });

    it("reads the runs, group, agent and baseline queries of /compare, as they are", () => {
        expect(pageOf("/compare", "?runs=a,b")).toEqual({ kind: "compare", runs: "a,b", group: "", agent: "", baseline: "" });
        expect(pageOf("/compare/", "?runs=a,b&x=1")).toEqual({ kind: "compare", runs: "a,b", group: "", agent: "", baseline: "" });
        expect(pageOf("/compare", "?group=sweep-1&agent=my+bot")).toEqual({
            kind: "compare",
            runs: "",
            group: "sweep-1",
            agent: "my bot",
            baseline: "",
        });
        expect(pageOf("/compare", "?runs=a,b&baseline=b")).toEqual({ kind: "compare", runs: "a,b", group: "", agent: "", baseline: "b" });
        expect(pageOf("/compare")).toEqual({ kind: "compare", runs: "", group: "", agent: "", baseline: "" });
    });

    it("no longer knows /variants", () => {
        expect(pageOf("/variants", "?runs=a,b")).toEqual({ kind: "unknown" });
        expect(pageOf("/variants/", "?group=sweep-1")).toEqual({ kind: "unknown" });
    });

    it("opens the Versions page at /agents/<name> and /agents/<name>/versions, the name decoded", () => {
        expect(pageOf("/agents/support-bot")).toEqual({ kind: "agentVersions", name: "support-bot" });
        expect(pageOf("/agents/my%20bot/")).toEqual({ kind: "agentVersions", name: "my bot" });
        expect(pageOf("/agents/support-bot/versions")).toEqual({ kind: "agentVersions", name: "support-bot" });
        expect(pageOf("/agents/my%20bot/versions/")).toEqual({ kind: "agentVersions", name: "my bot" });
    });

    it("reads the name of /agents/<name>/evaluation, decoded", () => {
        expect(pageOf("/agents/my%20bot/evaluation")).toEqual({ kind: "agentEvaluation", name: "my bot" });
        expect(pageOf("/agents/bot/evaluation/")).toEqual({ kind: "agentEvaluation", name: "bot" });
        expect(pageOf("/agents/my%20bot/monitoring")).toEqual({ kind: "agentMonitoring", name: "my bot" });
        expect(pageOf("/agents/bot/monitoring/")).toEqual({ kind: "agentMonitoring", name: "bot" });
    });

    it("reads the two versions of /agents/<name>/versions/compare", () => {
        const other = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";
        expect(pageOf("/agents/my%20bot/versions/compare", `?a=${ID}&b=${other}`)).toEqual({
            kind: "versionCompare",
            name: "my bot",
            a: ID,
            b: other,
        });
        expect(pageOf("/agents/bot/versions/compare/", `?b=${ID}&a=${other}`)).toEqual({
            kind: "versionCompare",
            name: "bot",
            a: other,
            b: ID,
        });
    });

    it("does not open the version comparison without two guids", () => {
        expect(pageOf("/agents/bot/versions/compare")).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/bot/versions/compare", `?a=${ID}`)).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/bot/versions/compare", `?a=${ID}&b=x`)).toEqual({ kind: "unknown" });
    });

    it("reads the agent and version of /agents/<name>/versions/<guid>", () => {
        expect(pageOf(`/agents/my%20bot/versions/${ID}`)).toEqual({ kind: "version", name: "my bot", versionId: ID });
    });

    it("is unknown for every other path", () => {
        expect(pageOf("/runs")).toEqual({ kind: "unknown" });
        expect(pageOf("/runs/abc")).toEqual({ kind: "unknown" });
        expect(pageOf(`/runs/${ID}/extra`)).toEqual({ kind: "unknown" });
        expect(pageOf(`/other/${ID}`)).toEqual({ kind: "unknown" });
        expect(pageOf("/agents")).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/bot/evaluation/extra")).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/bot/versions/abc")).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/%E0%A4%A")).toEqual({ kind: "unknown" });
    });
});
