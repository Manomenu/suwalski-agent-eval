// This feature's HTTP call, typed from the generated OpenAPI types. The server counts every check, tag and turn time.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import type { ValidSelection } from "./variants";

export type RunVariants = components["schemas"]["RunVariants"];
export type VariantRun = components["schemas"]["VariantRun"];
export type VariantScenario = components["schemas"]["VariantScenario"];
export type VariantCell = components["schemas"]["VariantCell"];

/** The runs as variants; an `Error` with the server's words when they cannot be shown (another agent's run, an unknown group). */
export function fetchVariants(selection: ValidSelection, signal: AbortSignal): Promise<RunVariants> {
    const query = new URLSearchParams(selection.kind === "runs" ? selection.ids.map((id) => ["runs", id]) : [["group", selection.group]]);
    if (selection.kind === "group" && selection.agent !== null) query.set("agent", selection.agent);
    return request<RunVariants>(`/runs/variants?${query.toString()}`, { signal });
}
