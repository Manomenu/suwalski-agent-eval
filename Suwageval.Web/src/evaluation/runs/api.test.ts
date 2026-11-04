import { describe, expect, it } from "vitest";

import { runRequestLike, type RunSummary } from "./api";

describe("runRequestLike", () => {
    it("AC-product-simplification-1: asks for the same tags, endpoint and group as the run it starts again", () => {
        const run = {
            agent: "a",
            tags: ["smoke"],
            endpointUrl: "http://agent.test/",
            group: "sweep-1",
            origin: null,
        } as unknown as RunSummary;

        expect(runRequestLike(run)).toEqual({
            agent: "a",
            tags: ["smoke"],
            endpointUrl: "http://agent.test/",
            group: "sweep-1",
            origin: null,
        });
    });
});
