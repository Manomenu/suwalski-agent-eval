import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentErrorsPath } from "./agentErrors";

export type AgentError = components["schemas"]["AgentError"];

export function fetchAgentErrors(
    name: string,
    versionIds: readonly string[],
    dataVersions: readonly string[],
    signal: AbortSignal,
): Promise<AgentError[]> {
    return request<AgentError[]>(agentErrorsPath(name, versionIds, dataVersions), { signal });
}
