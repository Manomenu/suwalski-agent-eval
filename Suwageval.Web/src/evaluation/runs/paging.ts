export const RUNS_PAGE_SIZE = 20;

export function runsPath(
    agent: string,
    page: number,
    pageSize: number,
    versionIds: readonly string[],
    group: string | null = null,
    grouping: "None" | "Any" | null = null,
    tags: readonly string[] = [],
    dataVersions: readonly string[] = [],
): string {
    const query = new URLSearchParams({ agent, page: String(page), pageSize: String(pageSize) });
    for (const id of versionIds) query.append("version", id);
    if (group !== null) query.set("group", group);
    if (grouping !== null) query.set("grouping", grouping);
    for (const tag of tags) query.append("tag", tag);
    for (const data of dataVersions) query.append("dataVersion", data);
    return `/eval/runs?${query.toString()}`;
}
