import { describe, expect, it } from "vitest";

import { settledView, settledWith } from "./request";

describe("settledView", () => {
    it("is loading and pending before the first answer", () => {
        expect(settledView(null, "a")).toEqual({ kind: "loading", pending: true });
    });

    it("asks nothing for a null key", () => {
        expect(settledView({ key: "a", result: { kind: "loaded", value: 1 } }, null)).toEqual({ kind: "loading", pending: false });
    });

    it("shows the answer for the key", () => {
        expect(settledView({ key: "a", result: { kind: "loaded", value: 1 } }, "a")).toEqual({ kind: "loaded", value: 1, pending: false });
    });

    it("keeps the previous answer, pending, while a new key loads", () => {
        expect(settledView({ key: "a", result: { kind: "loaded", value: 1 } }, "b")).toEqual({ kind: "loaded", value: 1, pending: true });
    });
});

describe("settledWith", () => {
    it("gives the error when there is no fallback", () => {
        expect(settledWith(new Error("down"), undefined)).toEqual({ kind: "error", error: new Error("down") });
    });

    it("wraps what is not an Error", () => {
        expect(settledWith("x", undefined)).toEqual({ kind: "error", error: new Error("x") });
    });

    it("gives the fallback instead of an error", () => {
        expect(settledWith(new Error("down"), [])).toEqual({ kind: "loaded", value: [] });
    });
});
