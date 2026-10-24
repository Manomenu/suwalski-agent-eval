// This feature's HTTP calls, typed from the generated OpenAPI types. The server lines the runs up and computes every count and change.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import type { ValidSelection } from "./variants";

export type RunComparison = components["schemas"]["RunComparison"];
export type BaselineComparison = components["schemas"]["BaselineComparison"];
export type ComparedCheck = components["schemas"]["ComparedCheck"];
export type ComparedRun = components["schemas"]["ComparedRun"];
export type ComparedRate = components["schemas"]["ComparedRate"];
export type ComparedTag = components["schemas"]["ComparedTag"];
export type RateChange = components["schemas"]["RateChange"];
export type TimeChange = components["schemas"]["TimeChange"];
export type Trend = components["schemas"]["Trend"];
export type VariantRun = components["schemas"]["VariantRun"];
export type VariantScenario = components["schemas"]["VariantScenario"];
export type VariantCell = components["schemas"]["VariantCell"];
export type VariantCheck = components["schemas"]["VariantCheck"];

/**
 * The runs (named one by one, or those of a group) as columns with a cell per scenario; with a baseline (one of the runs) also every
 * change against it. An `Error` with the server's words when they cannot be shown.
 */
export function fetchComparison(selection: ValidSelection, baseline: string | null, signal: AbortSignal): Promise<RunComparison> {
    const query = new URLSearchParams(selection.kind === "runs" ? selection.ids.map((id) => ["runs", id]) : [["group", selection.group]]);
    if (selection.kind === "group" && selection.agent !== null) query.set("agent", selection.agent);
    if (baseline !== null) query.set("baseline", baseline);
    return request<RunComparison>(`/runs/compare?${query.toString()}`, { signal });
}

/** Sets the note of the agent's run group (what the experiment is about); null clears it. */
export function setGroupNote(agent: string, group: string, note: string | null): Promise<void> {
    const path = `/agents/${encodeURIComponent(agent)}/run-groups/${encodeURIComponent(group)}/note`;
    return request<undefined>(path, { method: "PUT", body: JSON.stringify({ note }) }).then(() => undefined);
}
