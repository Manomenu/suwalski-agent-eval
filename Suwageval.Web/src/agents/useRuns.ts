import { useEffect, useRef, useState } from "react";

import { fetchRuns, type RunSummary } from "../runs/api";

export interface RunsState {
    runs: RunSummary[];
    /** Whether an older page exists. */
    hasMore: boolean;
    /** The first page (or a reload) is on its way: `runs` is what was loaded before, if anything. */
    loading: boolean;
    /** "Load more" is on its way. */
    loadingMore: boolean;
    error: string | null;
    loadMore: () => void;
}

/** What was loaded for one `key`. */
interface Loaded {
    key: string;
    runs: RunSummary[];
    next: string | null;
    error: string | null;
}

/** The version ids of the comma-joined list; none for null (all versions) or "" (no version). */
const idsOf = (versionIds: string | null) => (versionIds === null || versionIds === "" ? [] : versionIds.split(","));

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

/**
 * The agent's runs, newest first, a page at a time. `versionIds` (comma-separated) limits them to those versions (null: all; empty: none, nothing is
 * asked). A change of `key` (it must change with `agent` and when a reload is wanted; `versionIds` changing starts again too) starts again from the first
 * page, keeping the runs shown until it arrives; `loadMore` appends the next page.
 * The runs are read once per key, not polled: a run that is still going shows the state it had when the page loaded.
 */
export function useRuns(agent: string, versionIds: string | null, key: string): RunsState {
    const [loaded, setLoaded] = useState<Loaded | null>(null);
    // The key whose "Load more" is on its way.
    const [loadingMoreKey, setLoadingMoreKey] = useState<string | null>(null);
    // The request in flight: aborted when the key changes or the page goes away, so a late answer never lands.
    const controller = useRef<AbortController | null>(null);
    const none = versionIds === "";
    const ids = idsOf(versionIds);

    useEffect(() => {
        if (none) return;
        const current = new AbortController();
        controller.current = current;
        // Split here, from the string: the effect depends on `versionIds`, not on a new array of each render.
        fetchRuns(agent, null, idsOf(versionIds), current.signal)
            .then((page) => {
                if (!current.signal.aborted) setLoaded({ key, runs: page.runs, next: page.next ?? null, error: null });
            })
            .catch((e: unknown) => {
                if (!current.signal.aborted) setLoaded({ key, runs: [], next: null, error: message(e) });
            });
        return () => {
            current.abort();
        };
    }, [agent, key, none, versionIds]);

    const current = loaded?.key === key ? loaded : null;
    const loadingMore = loadingMoreKey === key;

    function loadMore() {
        const signal = controller.current?.signal;
        if (current?.next == null || loadingMore || signal === undefined) return;
        const { next } = current;
        setLoadingMoreKey(key);
        setLoaded({ ...current, error: null });
        fetchRuns(agent, next, ids, signal)
            .then((page) => {
                if (!signal.aborted) setLoaded({ key, runs: [...current.runs, ...page.runs], next: page.next ?? null, error: null });
            })
            .catch((e: unknown) => {
                if (!signal.aborted) setLoaded({ ...current, error: message(e) });
            })
            .finally(() => {
                if (!signal.aborted) setLoadingMoreKey(null);
            });
    }

    // While a new key loads, the previous runs stay; with no version allowed there is nothing to show or ask for.
    const shown = none ? null : loaded;
    return {
        runs: shown?.runs ?? [],
        hasMore: current?.next != null && !none,
        loading: !none && current === null,
        loadingMore,
        error: none ? null : (shown?.error ?? null),
        loadMore,
    };
}
