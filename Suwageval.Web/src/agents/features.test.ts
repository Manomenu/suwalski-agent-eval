import { describe, expect, it } from "vitest";

import { areaOffText, areaOn } from "./features";

describe("areaOn", () => {
    it("has evaluation on unless switched off and monitoring only with its settings", () => {
        expect(areaOn({}, "evaluation")).toBe(true);
        expect(areaOn({ evaluation: false }, "evaluation")).toBe(false);
        expect(areaOn({}, "monitoring")).toBe(false);
        expect(areaOn({ monitoring: {} }, "monitoring")).toBe(true);
    });
});

describe("areaOffText", () => {
    it("names the key of agent.yaml that switches it on", () => {
        expect(areaOffText("monitoring")).toBe(
            "Monitoring is off for this agent: features.monitoring in its suwageval/agent.yaml (then suwageval push).",
        );
    });
});
