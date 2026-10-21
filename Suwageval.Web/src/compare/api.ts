// This feature's HTTP calls, typed from the generated OpenAPI types. The server lines the runs up and computes every count and change.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import type { ValidSelection } from "./variants";

export type RunComparison = components["schemas"]["RunComparison"];
export type ComparedRun = components["schemas"]["ComparedRun"];
export type ComparedRate = components["schemas"]["ComparedRate"];
export type ComparedTag = components["schemas"]["ComparedTag"];
export type RateChange = components["schemas"]["RateChange"];
export type TimeChange = components["schemas"]["TimeChange"];
export type Trend = components["schemas"]["Trend"];
export type RunVariants = components["schemas"]["RunVariants"];
export type VariantRun = components["schemas"]["VariantRun"];
export type VariantScenario = components["schemas"]["VariantScenario"];
export type VariantCell = components["schemas"]["VariantCell"];
export type VariantCheck = components["schemas"]["VariantCheck"];

/** The runs side by side with every change against the first, the baseline; an `Error` with the server's words when they cannot be compared. */
export function fetchComparison(runIds: readonly string[], signal: AbortSignal): Promise<RunComparison> {
    const query = new URLSearchParams(runIds.map((id) => ["runs", id]));
    return request<RunComparison>(`/runs/compare?${query.toString()}`, { signal });
}

/** The runs (named one by one, or those of a group) as columns with a cell per scenario; an `Error` with the server's words when they cannot be shown. */
export function fetchVariants(selection: ValidSelection, signal: AbortSignal): Promise<RunVariants> {
    const query = new URLSearchParams(selection.kind === "runs" ? selection.ids.map((id) => ["runs", id]) : [["group", selection.group]]);
    if (selection.kind === "group" && selection.agent !== null) query.set("agent", selection.agent);
    return request<RunVariants>(`/runs/variants?${query.toString()}`, { signal });
}
