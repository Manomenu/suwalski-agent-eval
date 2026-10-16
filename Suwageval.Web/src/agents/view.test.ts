import { describe, expect, it } from "vitest";

import { ApiError } from "../api/client";
import { agentHref, isNotFound, jsonText, runHref, shortId, versionHref, versionLabel, versionTitle } from "./view";

describe("links", () => {
    it("encodes the agent name", () => {
        expect(agentHref("my bot")).toBe("/agents/my%20bot");
        expect(versionHref("my bot", "abc")).toBe("/agents/my%20bot/versions/abc");
        expect(runHref("abc")).toBe("/runs/abc");
    });
});

describe("isNotFound", () => {
    it("is true only for a 404 from the server", () => {
        expect(isNotFound(new ApiError("x", 404))).toBe(true);
        expect(isNotFound(new ApiError("x", 500))).toBe(false);
        expect(isNotFound(new Error("x"))).toBe(false);
    });
});

describe("versionLabel", () => {
    it("shows a dash without a label", () => {
        expect(versionLabel({ label: "v2" })).toBe("v2");
        expect(versionLabel({ label: null })).toBe("—");
        expect(versionLabel({ label: "  " })).toBe("—");
    });
});

describe("jsonText", () => {
    it("indents", () => {
        expect(jsonText({ a: 1 })).toBe('{\n  "a": 1\n}');
    });
});

describe("shortId", () => {
    it("keeps eight characters", () => {
        expect(shortId("3f2b8c1e-5d4a")).toBe("3f2b8c1e");
    });
});

describe("versionTitle", () => {
    it("is the label, or the start of the id without one", () => {
        expect(versionTitle("3f2b8c1e-5d4a", "v2")).toBe("v2");
        expect(versionTitle("3f2b8c1e-5d4a", null)).toBe("3f2b8c1e");
    });
});
