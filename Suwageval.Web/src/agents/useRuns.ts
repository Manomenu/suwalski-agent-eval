import { useEffect, useState } from "react";

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

/** What was loaded for one `key`. */
interface Loaded {
    key: string;
    runs: RunSummary[];
    totalCount: number;
    page: number;
    error: string | null;
}

/** The version ids of the comma-joined list; none for null (all versions) or "" (no version). */
const idsOf = (versionIds: string | null) => (versionIds === null || versionIds === "" ? [] : versionIds.split(","));

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

/**
 * One page of the agent's runs, newest first. `versionIds` (comma-separated) limits them to those versions (null: all; empty: none, nothing is
 * asked); `filter` limits them by group. A change of `key` (it must change with `agent` and when a reload is wanted), of the page or of a filter asks
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
): RunsState {
    const [loaded, setLoaded] = useState<Loaded | null>(null);
    const none = versionIds === "";
    const { group, grouping } = groupQuery(filter);
    const requestKey = JSON.stringify([agent, key, requestedPage, pageSize, versionIds, group, grouping]);

    useEffect(() => {
        if (none) return;
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        // Split here, from the string: the effect depends on `versionIds`, not on a new array of each render.
        fetchRuns(agent, requestedPage, pageSize, idsOf(versionIds), group, grouping, controller.signal)
            .then((list) => {
                if (!controller.signal.aborted)
                    setLoaded({ key: requestKey, runs: list.runs, totalCount: list.totalCount, page: list.page, error: null });
            })
            .catch((e: unknown) => {
                if (!controller.signal.aborted)
                    setLoaded({ key: requestKey, runs: [], totalCount: 0, page: requestedPage, error: message(e) });
            });
        return () => {
            controller.abort();
        };
    }, [requestKey]); // eslint-disable-line react-hooks/exhaustive-deps -- `requestKey` says what is asked for; `versionIds` is a string, the rest follows from it.

    // While a new request loads, the previous runs stay; with no version allowed there is nothing to show or ask for.
    const shown = none ? null : loaded;
    return {
        runs: shown?.runs ?? [],
        totalCount: shown?.totalCount ?? 0,
        page: shown?.page ?? requestedPage,
        loading: !none && loaded?.key !== requestKey,
        error: none ? null : (shown?.error ?? null),
    };
}
