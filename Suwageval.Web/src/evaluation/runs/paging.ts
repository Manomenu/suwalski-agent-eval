import type { paths } from "../../api/openapi";

export const RUNS_PAGE_SIZE = 20;

export type RunsQuery = NonNullable<paths["/eval/runs"]["get"]["parameters"]["query"]>;

export function runsQuery(
    agent: string,
    page: number,
    pageSize: number,
    versionIds: readonly string[],
    group: string | null = null,
    grouping: "None" | "Any" | null = null,
    tags: readonly string[] = [],
    dataVersions: readonly string[] = [],
): RunsQuery {
    return {
        agent,
        page,
        pageSize,
        ...(versionIds.length > 0 && { version: [...versionIds] }),
        ...(group !== null && { group }),
        ...(grouping !== null && { grouping }),
        ...(tags.length > 0 && { tag: [...tags] }),
        ...(dataVersions.length > 0 && { dataVersion: [...dataVersions] }),
    };
}
