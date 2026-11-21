import { queryOptions } from "@tanstack/react-query";
import { dataOf } from "@maniumek/suwfrontkit";

import { agentKeys, fetchVersion } from "../../agents/api";
import { api } from "../../api/client";
import type { components } from "../../api/openapi";
import { type ValidSelection } from "./variants";

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

function comparisonParams(selection: ValidSelection, baseline: string | null) {
    return {
        ...(selection.kind === "runs" ? { runs: selection.ids } : { group: selection.group }),
        ...(selection.kind === "group" && selection.agent !== null ? { agent: selection.agent } : {}),
        ...(baseline === null ? {} : { baseline }),
    };
}

function fetchComparison(selection: ValidSelection, baseline: string | null, signal: AbortSignal): Promise<RunComparison> {
    return dataOf(api.GET("/eval/runs/compare", { params: { query: comparisonParams(selection, baseline) }, signal }));
}

export const compareKeys = {
    all: ["compare"] as const,
    selection: (selection: ValidSelection, baseline: string | null) => ["compare", selection, baseline] as const,
};

export const comparisonQuery = (selection: ValidSelection, baseline: string | null) =>
    queryOptions({
        queryKey: compareKeys.selection(selection, baseline),
        queryFn: ({ signal }) => fetchComparison(selection, baseline, signal),
    });

export const comparedVersionsQuery = (agent: string, versionIds: string[]) =>
    queryOptions({
        queryKey: [...agentKeys.versions(agent), "compared", ...versionIds] as const,
        queryFn: ({ signal }) => Promise.all(versionIds.map((id) => fetchVersion(agent, id, signal))),
    });

export async function setGroupNote(agent: string, group: string, note: string | null): Promise<void> {
    await dataOf(api.PUT("/eval/agents/{agent}/run-groups/{name}/note", { params: { path: { agent, name: group } }, body: { note } }));
}
