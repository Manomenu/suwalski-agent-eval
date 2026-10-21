// This feature's HTTP call, typed from the generated OpenAPI types. The server lines the runs up and computes every change.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type RunComparison = components["schemas"]["RunComparison"];
export type ComparedRun = components["schemas"]["ComparedRun"];
export type ComparedCheck = components["schemas"]["ComparedCheck"];
export type ComparedCell = components["schemas"]["ComparedCell"];
export type RateChange = components["schemas"]["RateChange"];
export type TimeChange = components["schemas"]["TimeChange"];
export type Trend = components["schemas"]["Trend"];

/** The runs side by side, the first one the baseline; an `Error` with the server's words when they cannot be compared. */
export function fetchComparison(runIds: readonly string[], signal: AbortSignal): Promise<RunComparison> {
    const query = new URLSearchParams(runIds.map((id) => ["runs", id]));
    return request<RunComparison>(`/runs/compare?${query.toString()}`, { signal });
}
