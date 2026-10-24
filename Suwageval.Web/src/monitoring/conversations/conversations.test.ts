import { describe, expect, it } from "vitest";

import { conversationsPath, stateOf, versionText } from "./conversations";

describe("conversationsPath", () => {
    it("asks for one page, narrowed by the filter", () => {
        expect(conversationsPath("my bot", { environment: null, state: null }, 1, 20)).toBe(
            "/monitoring/agents/my%20bot/conversations?page=1&pageSize=20",
        );
        expect(conversationsPath("a", { environment: "production", state: "Closed" }, 3, 20)).toBe(
            "/monitoring/agents/a/conversations?page=3&pageSize=20&environment=production&state=Closed",
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
