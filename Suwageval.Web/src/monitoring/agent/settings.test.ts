import { describe, expect, it } from "vitest";

import { monitoringSettingsText } from "./settings";

describe("monitoringSettingsText", () => {
    it("says the defaults when nothing is set", () => {
        expect(monitoringSettingsText({})).toBe("A conversation closes after 30m without a turn · environments: any");
    });

    it("names what is set", () => {
        expect(monitoringSettingsText({ closeAfter: "2h", environments: ["production", "staging"] })).toBe(
            "A conversation closes after 2h without a turn · environments: production, staging",
        );
    });
});
