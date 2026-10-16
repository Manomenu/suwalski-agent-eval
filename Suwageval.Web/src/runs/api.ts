// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type RunResults = components["schemas"]["RunResults"];
export type RunSummary = components["schemas"]["RunSummary"];
export type ScenarioRunResult = components["schemas"]["ScenarioRunResult"];
export type CheckRunResult = components["schemas"]["CheckRunResult"];
export type ConversationItem = components["schemas"]["ConversationItem"];

export function fetchRunResults(runId: string, signal: AbortSignal): Promise<RunResults> {
    return request<RunResults>(`/runs/${runId}/results`, { signal });
}
