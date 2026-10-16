import { describe, expect, it } from "vitest";

import { runIdOf } from "./route";

const ID = "3f2b8c1e-5d4a-4e6f-9a7b-0c1d2e3f4a5b";

describe("runIdOf", () => {
    it("reads the guid of /runs/<guid>", () => {
        expect(runIdOf(`/runs/${ID}`)).toBe(ID);
        expect(runIdOf(`/runs/${ID}/`)).toBe(ID);
    });

    it("is null for every other path", () => {
        expect(runIdOf("/")).toBeNull();
        expect(runIdOf("/runs")).toBeNull();
        expect(runIdOf("/runs/abc")).toBeNull();
        expect(runIdOf(`/runs/${ID}/extra`)).toBeNull();
        expect(runIdOf(`/other/${ID}`)).toBeNull();
    });
});
