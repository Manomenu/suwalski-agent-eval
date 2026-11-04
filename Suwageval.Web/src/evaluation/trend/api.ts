import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { agentPath } from "../../agents/api";

export type RunPassRates = components["schemas"]["RunPassRates"];

export function fetchPassRates(name: string, signal: AbortSignal): Promise<RunPassRates[]> {
    return request<RunPassRates[]>(`/eval${agentPath(name)}/pass-rates`, { signal });
}
