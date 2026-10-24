import { describe, expect, it } from "vitest";

import { readWhatDiffersOpen, saveWhatDiffersOpen, whatDiffersTitle } from "./whatDiffersOpening";

function memoryStorage() {
    const values = new Map<string, string>();
    return {
        getItem: (key: string) => values.get(key) ?? null,
        setItem: (key: string, value: string) => {
            values.set(key, value);
        },
    };
}

const failing = {
    getItem: (): string | null => {
        throw new Error("blocked");
    },
    setItem: () => {
        throw new Error("full");
    },
};

describe("readWhatDiffersOpen", () => {
    it("is folded when nothing is stored, the storage is missing or it fails", () => {
        expect(readWhatDiffersOpen(memoryStorage())).toBe(false);
        expect(readWhatDiffersOpen(null)).toBe(false);
        expect(readWhatDiffersOpen(failing)).toBe(false);
    });

    it("reads back what was saved", () => {
        const storage = memoryStorage();
        saveWhatDiffersOpen(true, storage);
        expect(readWhatDiffersOpen(storage)).toBe(true);
        saveWhatDiffersOpen(false, storage);
        expect(readWhatDiffersOpen(storage)).toBe(false);
    });

    it("is folded for a value it did not write", () => {
        const storage = memoryStorage();
        storage.setItem("suwageval.compare.whatDiffersOpen", "yes");
        expect(readWhatDiffersOpen(storage)).toBe(false);
    });
});

describe("saveWhatDiffersOpen", () => {
    it("ignores a storage that fails", () => {
        expect(() => {
            saveWhatDiffersOpen(true, failing);
        }).not.toThrow();
    });
});

describe("whatDiffersTitle", () => {
    it("counts the rows once they are known", () => {
        expect(whatDiffersTitle(null)).toBe("What differs");
        expect(whatDiffersTitle(0)).toBe("What differs · 0");
        expect(whatDiffersTitle(7)).toBe("What differs · 7");
    });
});
