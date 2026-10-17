import { describe, expect, it } from "vitest";

import { AGENT_ERROR_MAX_LENGTH, AGENT_ERRORS_MAX_ROWS, errorRows, errorsOfSamples, shortError } from "./agentErrors";
import type { AgentError } from "./api";

const item = (over: Partial<AgentError> = {}): AgentError => ({
    scenarioKey: "refund",
    agentVersionId: "v1",
    errors: 3,
    samples: 12,
    newestErrorRunId: "r1",
    newestError: "HTTP 500",
    ...over,
});

describe("errorsOfSamples", () => {
    it("is the errors over the samples asked", () => {
        expect(errorsOfSamples(item())).toBe("3/12");
    });
});

describe("errorRows", () => {
    it("keeps the first rows in the server's order", () => {
        const list = Array.from({ length: AGENT_ERRORS_MAX_ROWS + 2 }, (_, i) => item({ scenarioKey: `s${i}` }));
        expect(errorRows(list)).toEqual(list.slice(0, AGENT_ERRORS_MAX_ROWS));
    });
});

describe("shortError", () => {
    it("puts the text on one line and keeps a short one whole", () => {
        expect(shortError("HTTP 500\n  Internal   Server Error")).toBe("HTTP 500 Internal Server Error");
    });

    it("cuts a long text to the limit with an ellipsis", () => {
        const cut = shortError("x".repeat(AGENT_ERROR_MAX_LENGTH * 2));
        expect(cut).toHaveLength(AGENT_ERROR_MAX_LENGTH);
        expect(cut.endsWith("…")).toBe(true);
    });
});
