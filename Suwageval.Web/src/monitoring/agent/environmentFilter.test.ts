import { describe, expect, it } from "vitest";

import { environmentOf, searchWithEnvironment } from "./environmentFilter";

describe("environmentOf", () => {
    it("reads the environment, none being all of them", () => {
        expect(environmentOf("?environment=staging")).toBe("staging");
        expect(environmentOf("?environment=")).toBeNull();
        expect(environmentOf("")).toBeNull();
    });
});

describe("searchWithEnvironment", () => {
    it("sets or clears the environment, starts the conversations from their first page and keeps the rest", () => {
        expect(searchWithEnvironment("?failedCheck=a&conversationsPage=3", "staging")).toBe("?failedCheck=a&environment=staging");
        expect(searchWithEnvironment("?environment=staging", null)).toBe("");
    });
});
