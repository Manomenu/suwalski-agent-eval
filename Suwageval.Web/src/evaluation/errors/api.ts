// This sub-feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentErrorsPath } from "./agentErrors";

export type AgentError = components["schemas"]["AgentError"];

/** The open scenarios where the agent gave no answer, per version, worst first (see `agentErrors.ts`), decided over the given versions. */
export function fetchAgentErrors(name: string, versionIds: readonly string[], signal: AbortSignal): Promise<AgentError[]> {
    return request<AgentError[]>(agentErrorsPath(name, versionIds), { signal });
}
