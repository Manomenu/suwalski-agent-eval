import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { isNotFound } from "./client";

describe("isNotFound", () => {
    it("is true only for a 404 from the server", () => {
        expect(isNotFound(new ApiError(404, { detail: "x" }))).toBe(true);
        expect(isNotFound(new ApiError(500, { detail: "x" }))).toBe(false);
        expect(isNotFound(new Error("x"))).toBe(false);
    });
});
