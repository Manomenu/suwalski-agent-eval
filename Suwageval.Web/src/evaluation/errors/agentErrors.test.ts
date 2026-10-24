import { describe, expect, it } from "vitest";

import { AGENT_ERROR_MAX_LENGTH, agentErrorsPath, errorsOfSamples, shortError } from "./agentErrors";
import { type AgentError } from "./api";

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

describe("agentErrorsPath", () => {
    it("asks the default versions without a query and the given ones with repeated parameters", () => {
        expect(agentErrorsPath("my agent", [])).toBe("/eval/agents/my%20agent/agent-errors");
        expect(agentErrorsPath("a", ["v1", "v2"])).toBe("/eval/agents/a/agent-errors?version=v1&version=v2");
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
