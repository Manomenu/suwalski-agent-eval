import { describe, expect, it } from "vitest";

import { compareVersionsTitle, jsonText, versionLabel, versionsToCompare } from "./view";

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

describe("versionsToCompare", () => {
    const versions = [{ id: "a" }, { id: "b" }, { id: "c" }];

    it("gives the two selected versions in the list's order", () => {
        expect(versionsToCompare(versions, new Set(["c", "a"]))).toEqual(["a", "c"]);
    });

    it("gives nothing for fewer or more than two", () => {
        expect(versionsToCompare(versions, new Set())).toBeNull();
        expect(versionsToCompare(versions, new Set(["a"]))).toBeNull();
        expect(versionsToCompare(versions, new Set(["a", "b", "c"]))).toBeNull();
    });

    it("ignores a selected version the list no longer shows", () => {
        expect(versionsToCompare(versions, new Set(["a", "gone"]))).toBeNull();
    });

    it("names the page and says why it cannot open yet", () => {
        expect(compareVersionsTitle(false)).toContain("exactly two");
        expect(compareVersionsTitle(true)).toContain("changed");
    });
});
