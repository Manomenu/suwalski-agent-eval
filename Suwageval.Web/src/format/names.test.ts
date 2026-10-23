import { describe, expect, it } from "vitest";

import { shortId, versionTitle } from "./names";

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
