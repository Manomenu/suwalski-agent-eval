import { describe, expect, it } from "vitest";

import { pageOf } from "./route";

const ID = "3f2b8c1e-5d4a-4e6f-9a7b-0c1d2e3f4a5b";

describe("pageOf", () => {
    it("opens the agent list at the root", () => {
        expect(pageOf("/")).toEqual({ kind: "agents" });
    });

    it("reads the guid of /runs/<guid>", () => {
        expect(pageOf(`/runs/${ID}`)).toEqual({ kind: "run", runId: ID });
        expect(pageOf(`/runs/${ID}/`)).toEqual({ kind: "run", runId: ID });
    });

    it("reads the name of /agents/<name>, decoded", () => {
        expect(pageOf("/agents/support-bot")).toEqual({ kind: "agent", name: "support-bot" });
        expect(pageOf("/agents/my%20bot/")).toEqual({ kind: "agent", name: "my bot" });
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
        expect(pageOf("/agents/bot/versions/abc")).toEqual({ kind: "unknown" });
        expect(pageOf("/agents/%E0%A4%A")).toEqual({ kind: "unknown" });
    });
});
