import { useRequest } from "../../api/useRequest";
import { fetchRuns, type RunSummary } from "../runs/api";
import { groupQuery, type GroupFilter } from "./groupFilter";

export interface RunsState {
    runs: RunSummary[];
    totalCount: number;
    page: number;
    loading: boolean;
    error: string | null;
}

const idsOf = (versionIds: string | null) => (versionIds === null || versionIds === "" ? [] : versionIds.split(","));

export function useRuns(
    agent: string,
    versionIds: string | null,
    key: string,
    filter: GroupFilter,
    requestedPage: number,
    pageSize: number,
    tags: readonly string[],
    dataVersions: readonly string[],
): RunsState {
    const none = versionIds === "";
    const { group, grouping } = groupQuery(filter);
    const requestKey = JSON.stringify([agent, key, requestedPage, pageSize, versionIds, group, grouping, tags, dataVersions]);
    // Why: split from the string because the request depends on versionIds, not on a new array every render
    const loaded = useRequest(none ? null : requestKey, (signal) =>
        fetchRuns(agent, requestedPage, pageSize, idsOf(versionIds), group, grouping, tags, dataVersions, signal),
    );

    return {
        runs: loaded.kind === "loaded" ? loaded.value.runs : [],
        totalCount: loaded.kind === "loaded" ? loaded.value.totalCount : 0,
        page: loaded.kind === "loaded" ? loaded.value.page : requestedPage,
        loading: loaded.pending,
        error: loaded.kind === "error" ? loaded.error.message : null,
    };
}
