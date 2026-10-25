import { describe, expect, it } from "vitest";

import { ApiError, isNotFound, toError } from "./client";

describe("toError", () => {
    it("carries the server's detail", async () => {
        const response = new Response(JSON.stringify({ detail: "not found" }), { status: 404 });

        const error = await toError(response);

        expect(error.message).toBe("not found");
        expect(error.status).toBe(404);
    });

    it("carries the field messages of a validation problem", async () => {
        const response = new Response(JSON.stringify({ errors: { runs: ["Choose at most 7 runs."], group: ["Bad group."] } }), {
            status: 422,
        });

        expect((await toError(response)).message).toBe("Choose at most 7 runs. Bad group.");
    });

    it("falls back to the status when the body says nothing", async () => {
        const response = new Response("oops", { status: 502, statusText: "Bad Gateway" });

        expect((await toError(response)).message).toBe("502 Bad Gateway");
    });
});

describe("isNotFound", () => {
    it("is true only for a 404 from the server", () => {
        expect(isNotFound(new ApiError("x", 404))).toBe(true);
        expect(isNotFound(new ApiError("x", 500))).toBe(false);
        expect(isNotFound(new Error("x"))).toBe(false);
    });
});
