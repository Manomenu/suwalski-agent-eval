// This sub-feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentPath } from "../../agents/api";

export type RunPassRates = components["schemas"]["RunPassRates"];

/** The pass rate of the agent's finished runs, oldest first (the newest 50 at most). */
export function fetchPassRates(name: string, signal: AbortSignal): Promise<RunPassRates[]> {
    return request<RunPassRates[]>(`${agentPath(name)}/pass-rates`, { signal });
}
