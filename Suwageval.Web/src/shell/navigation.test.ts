import { describe, expect, it } from "vitest";

import { navigationOf } from "./navigation";

describe("navigationOf", () => {
    it("lights Agents on the list", () => {
        expect(navigationOf({ kind: "agents" }, null)).toEqual({ agentName: null, active: "agents" });
    });

    it("lights Evaluation on the agent's Evaluation page", () => {
        expect(navigationOf({ kind: "agentEvaluation", name: "bot" }, null)).toEqual({ agentName: "bot", active: "evaluation" });
    });

    it("lights Versions on the versions list, a version and the comparison of versions", () => {
        const versions = { agentName: "bot", active: "versions" };
        expect(navigationOf({ kind: "agentVersions", name: "bot" }, null)).toEqual(versions);
        expect(navigationOf({ kind: "version", name: "bot", versionId: "v" }, null)).toEqual(versions);
        expect(navigationOf({ kind: "versionCompare", name: "bot", a: "a", b: "b" }, null)).toEqual(versions);
    });

    it("lights Evaluation of the agent a run page or the comparison reported", () => {
        const evaluation = { agentName: "bot", active: "evaluation" };
        expect(navigationOf({ kind: "run", runId: "r" }, "bot")).toEqual(evaluation);
        expect(navigationOf({ kind: "compare", runs: "", group: "", agent: "", baseline: "" }, "bot")).toEqual(evaluation);
    });

    it("lights nothing on a run page until its agent is known, nor on an unknown page", () => {
        expect(navigationOf({ kind: "run", runId: "r" }, null)).toEqual({ agentName: null, active: null });
        expect(navigationOf({ kind: "unknown" }, "bot")).toEqual({ agentName: null, active: null });
    });
});
