import { queryOptions, useQuery, useQueryClient } from "@tanstack/react-query";

import { fetchRunResults, fetchRunSummary, runKeys, type RunResults } from "./api";
import { POLL_INTERVAL_MS, isActive, needsFullResults } from "./results";

export type RunResultsState = { kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; results: RunResults };

export function useRunResults(runId: string): RunResultsState {
    const queryClient = useQueryClient();
    const query = useQuery(
        queryOptions({
            queryKey: runKeys.results(runId),
            queryFn: async ({ signal }) => {
                const cached = queryClient.getQueryData<RunResults>(runKeys.results(runId));
                if (cached === undefined || !isActive(cached.run.status)) return fetchRunResults(runId, signal);
                const summary = await fetchRunSummary(runId, signal);
                return needsFullResults(cached.run, summary) ? fetchRunResults(runId, signal) : cached;
            },
            refetchInterval: ({ state }) => (state.data !== undefined && isActive(state.data.run.status) ? POLL_INTERVAL_MS : false),
        }),
    );

    if (query.data !== undefined) return { kind: "loaded", results: query.data };
    if (query.isError) return { kind: "error", error: query.error };
    return { kind: "loading" };
}
