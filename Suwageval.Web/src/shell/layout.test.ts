import { describe, expect, it } from "vitest";

import { CONTENT_MAX_WIDTH, contentMaxWidth } from "./layout";

describe("contentMaxWidth", () => {
    it("lets the agent's page and the comparison take the whole screen, and keeps every other page narrower", () => {
        expect(contentMaxWidth("compare")).toBeUndefined();
        expect(contentMaxWidth("agent")).toBeUndefined();
        expect(contentMaxWidth("run")).toBe(CONTENT_MAX_WIDTH);
    });
});
