import { describe as group, expect, it } from "vitest";

import { describe, statusOf } from "./status";

group("statusOf", () => {
    it("is up when the server's health check passed", () => {
        expect(statusOf(null)).toEqual({ kind: "up" });
        expect(describe(statusOf(null))).toBe("up");
    });

    it("is down with the reason when the health check failed", () => {
        expect(describe(statusOf(new Error("503 Service Unavailable")))).toBe("down (503 Service Unavailable)");
    });
});
