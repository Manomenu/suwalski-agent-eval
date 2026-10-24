import { describe, expect, it } from "vitest";

import { openScenariosOf, searchWithOpenScenarios, toggledScenario } from "./openScenarios";

describe("open scenarios in the address", () => {
    it("reads each open scenario once, in order", () => {
        expect(openScenariosOf("?open=b&tag=x&open=a&open=b&open=")).toEqual(["b", "a"]);
    });

    it("replaces its own parameters and keeps the others", () => {
        expect(searchWithOpenScenarios("?tag=x&open=old", ["a", "b"])).toBe("?tag=x&open=a&open=b");
        expect(searchWithOpenScenarios("?open=old", [])).toBe("");
    });

    it("toggles a scenario", () => {
        expect(toggledScenario(["a"], "b")).toEqual(["a", "b"]);
        expect(toggledScenario(["a", "b"], "a")).toEqual(["b"]);
    });
});
