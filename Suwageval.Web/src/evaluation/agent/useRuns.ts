import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { runListQuery, type RunSummary } from "../runs/api";
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
    filter: GroupFilter,
    requestedPage: number,
    pageSize: number,
    tags: readonly string[],
    dataVersions: readonly string[],
): RunsState {
    const { group, grouping } = groupQuery(filter);

    const query = useQuery({
        ...runListQuery(agent, requestedPage, pageSize, idsOf(versionIds), group, grouping, tags, dataVersions),
        enabled: versionIds !== "",
        placeholderData: keepPreviousData,
    });

    return {
        runs: query.data?.runs ?? [],
        totalCount: query.data?.totalCount ?? 0,
        page: query.data?.page ?? requestedPage,
        loading: query.isFetching,
        error: query.isError ? query.error.message : null,
    };
}
