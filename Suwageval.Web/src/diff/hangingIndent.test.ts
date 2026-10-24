import { describe, expect, it } from "vitest";

import { hangingIndent, hangingIndents } from "./hangingIndent";

describe("hangingIndent", () => {
    it("hangs under the value: indentation, list dashes and the key", () => {
        expect(hangingIndent("criteria: The answer tells")).toBe("criteria: ".length);
        expect(hangingIndent("    description: Says the item")).toBe("    description: ".length);
        expect(hangingIndent("  - kettle arrived broken")).toBe("  - ".length);
        expect(hangingIndent("- key: x")).toBe("- key: ".length);
        expect(hangingIndent('  "a:b": value')).toBe('  "a:b": '.length);
    });

    it("hangs only by the indentation when the line has no key", () => {
        expect(hangingIndent("  plain text: no, a key")).toBe("  plain text: ".length);
        expect(hangingIndent("    just text")).toBe(4);
        expect(hangingIndent("pattern: (?i)^a:b$")).toBe("pattern: ".length);
    });
});

describe("hangingIndents", () => {
    it("hangs the text of a block scalar by its indentation only, never by a word followed by a colon", () => {
        const lines = [
            "continuation:",
            "  turns: 6",
            "  simulatedUser: |",
            "    You are Marek. You are annoyed and push hard: you demand a manager.",
            "",
            "    Short messages: in English.",
            "checks:",
            "  - criteria: The agent stays polite: always.",
        ];
        expect(hangingIndents(lines)).toEqual([
            "continuation:".length,
            "  turns: ".length,
            "  simulatedUser: ".length,
            4,
            0,
            4,
            "checks:".length,
            "  - criteria: ".length,
        ]);
    });

    it("knows the other block scalar headers: folded, chomped, in a list", () => {
        expect(hangingIndents(["config: >-", "  a: b", "- |", "  c: d", "e: f"])).toEqual([
            "config: ".length,
            2,
            "- ".length,
            2,
            "e: ".length,
        ]);
    });
});
