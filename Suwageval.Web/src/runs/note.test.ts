import { describe, expect, it } from "vitest";

import { NOTE_MAX_LENGTH, normalizeNote, noteProblem, shortNote } from "./note";

describe("normalizeNote", () => {
    it("trims, and a blank note is none", () => {
        expect(normalizeNote("  trying model X \n")).toBe("trying model X");
        expect(normalizeNote("   ")).toBeNull();
        expect(normalizeNote(null)).toBeNull();
        expect(normalizeNote(undefined)).toBeNull();
    });
});

describe("noteProblem", () => {
    it("accepts a note up to the limit and refuses a longer one", () => {
        expect(noteProblem("x".repeat(NOTE_MAX_LENGTH))).toBeNull();
        expect(noteProblem("x".repeat(NOTE_MAX_LENGTH + 1))).toContain(String(NOTE_MAX_LENGTH));
    });
    it("measures the trimmed note", () => {
        expect(noteProblem(` ${"x".repeat(NOTE_MAX_LENGTH)} `)).toBeNull();
    });
});

describe("shortNote", () => {
    it("keeps a short note on one line", () => {
        expect(shortNote("rebuilt\nthe  index", 80)).toBe("rebuilt the index");
    });
    it("cuts a long note to the limit with an ellipsis", () => {
        const short = shortNote("a".repeat(30), 10);
        expect(short).toBe(`${"a".repeat(9)}…`);
        expect(short).toHaveLength(10);
    });
});
