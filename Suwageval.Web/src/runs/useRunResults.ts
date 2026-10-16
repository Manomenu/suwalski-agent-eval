import { useEffect, useState } from "react";

import { fetchRunResults, type RunResults } from "./api";
import { POLL_INTERVAL_MS, isActive } from "./results";

export type RunResultsState = { kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; results: RunResults };

/** A run's results, asked again every few seconds until the run is over. */
export function useRunResults(runId: string): RunResultsState {
    const [state, setState] = useState<RunResultsState>({ kind: "loading" });

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first loop, ignore its results.
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout> | undefined;

        const load = () => {
            fetchRunResults(runId, controller.signal)
                .then((results) => {
                    setState({ kind: "loaded", results });
                    if (isActive(results.run.status)) timer = setTimeout(load, POLL_INTERVAL_MS);
                })
                .catch((e: unknown) => {
                    if (!controller.signal.aborted) setState({ kind: "error", error: e instanceof Error ? e : new Error(String(e)) });
                });
        };
        load();

        return () => {
            controller.abort();
            clearTimeout(timer);
        };
    }, [runId]);

    return state;
}
