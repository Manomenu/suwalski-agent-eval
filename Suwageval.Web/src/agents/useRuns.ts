import { useRequest } from "../api/useRequest";
import { fetchRuns, type RunSummary } from "../runs/api";
import { groupQuery, type GroupFilter } from "./groupFilter";

export interface RunsState {
    runs: RunSummary[];
    /** How many runs match the filters, over all pages. */
    totalCount: number;
    /** The page `runs` are: the requested one, or the last when that is past the end. */
    page: number;
    /** The page (or a reload) is on its way: `runs` is what was loaded before, if anything. */
    loading: boolean;
    error: string | null;
}

/** The version ids of the comma-joined list; none for null (all versions) or "" (no version). */
const idsOf = (versionIds: string | null) => (versionIds === null || versionIds === "" ? [] : versionIds.split(","));

/**
 * One page of the agent's runs, newest first. `versionIds` (comma-separated) limits them to those versions (null: all; empty: none, nothing is
 * asked); `filter` limits them by group; `tags` to the runs that ran the scenarios of any of them (none: no limit). A change of `key` (it must change with `agent` and when a reload is wanted), of the page or of a filter asks
 * again, keeping the runs shown until the new ones arrive.
 * The runs are read once per request, not polled: a run that is still going shows the state it had when the page loaded.
 */
export function useRuns(
    agent: string,
    versionIds: string | null,
    key: string,
    filter: GroupFilter,
    requestedPage: number,
    pageSize: number,
    tags: readonly string[],
): RunsState {
    const none = versionIds === "";
    const { group, grouping } = groupQuery(filter);
    const requestKey = JSON.stringify([agent, key, requestedPage, pageSize, versionIds, group, grouping, tags]);
    // Split here, from the string: the request depends on `versionIds`, not on a new array of each render.
    const loaded = useRequest(none ? null : requestKey, (signal) =>
        fetchRuns(agent, requestedPage, pageSize, idsOf(versionIds), group, grouping, tags, signal),
    );

    // While a new request loads, the previous runs stay; with no version allowed there is nothing to show or ask for.
    return {
        runs: loaded.kind === "loaded" ? loaded.value.runs : [],
        totalCount: loaded.kind === "loaded" ? loaded.value.totalCount : 0,
        page: loaded.kind === "loaded" ? loaded.value.page : requestedPage,
        loading: loaded.pending,
        error: loaded.kind === "error" ? loaded.error.message : null,
    };
}
