import { describe, expect, it } from "vitest";

import { NOTE_MAX_LENGTH, normalizeNote, noteProblem } from "./note";

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
