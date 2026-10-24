import { useEffect, useState } from "react";

import { fetchRunResults, fetchRunSummary, type RunResults } from "./api";
import { POLL_INTERVAL_MS, isActive, needsFullResults } from "./results";

export type RunResultsState = { kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; results: RunResults };

/**
 * A run's results. While the run goes on only its small summary is asked for every few seconds; the full results are fetched
 * again when the summary shows something new (`needsFullResults`), which includes the final status.
 */
export function useRunResults(runId: string): RunResultsState {
    const [state, setState] = useState<RunResultsState>({ kind: "loading" });

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first loop, ignore its results.
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout> | undefined;
        const fail = (e: unknown) => {
            if (!controller.signal.aborted) setState({ kind: "error", error: e instanceof Error ? e : new Error(String(e)) });
        };

        const loadResults = () => {
            fetchRunResults(runId, controller.signal)
                .then((results) => {
                    setState({ kind: "loaded", results });
                    if (isActive(results.run.status)) timer = setTimeout(() => poll(results), POLL_INTERVAL_MS);
                })
                .catch(fail);
        };
        const poll = (shown: RunResults) => {
            fetchRunSummary(runId, controller.signal)
                .then((summary) => {
                    if (needsFullResults(shown.run, summary)) loadResults();
                    else timer = setTimeout(() => poll(shown), POLL_INTERVAL_MS);
                })
                .catch(fail);
        };
        loadResults();

        return () => {
            controller.abort();
            clearTimeout(timer);
        };
    }, [runId]);

    return state;
}
