import { request } from "../../api/client";
import type { components } from "../../api/openapi";
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

export function fetchComparison(selection: ValidSelection, baseline: string | null, signal: AbortSignal): Promise<RunComparison> {
    const query = new URLSearchParams(selection.kind === "runs" ? selection.ids.map((id) => ["runs", id]) : [["group", selection.group]]);
    if (selection.kind === "group" && selection.agent !== null) query.set("agent", selection.agent);
    if (baseline !== null) query.set("baseline", baseline);
    return request<RunComparison>(`/eval/runs/compare?${query.toString()}`, { signal });
}

export function setGroupNote(agent: string, group: string, note: string | null): Promise<void> {
    const path = `/eval/agents/${encodeURIComponent(agent)}/run-groups/${encodeURIComponent(group)}/note`;
    return request<undefined>(path, { method: "PUT", body: JSON.stringify({ note }) }).then(() => undefined);
}
