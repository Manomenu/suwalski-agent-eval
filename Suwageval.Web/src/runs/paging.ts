// The query that asks the server for one page of runs.

/** The runs table's page size. */
export const RUNS_PAGE_SIZE = 20;

/**
 * The path of one page of an agent's runs, newest first: `page` is 1-based, `versionIds` limits the runs to those versions (none: all
 * of them), `group` to the runs started in that group, `grouping` to the runs in no group (`None`) or in some group (`Any`); none of
 * them: all runs.
 */
export function runsPath(
    agent: string,
    page: number,
    pageSize: number,
    versionIds: readonly string[],
    group: string | null = null,
    grouping: "None" | "Any" | null = null,
): string {
    const query = new URLSearchParams({ agent, page: String(page), pageSize: String(pageSize) });
    for (const id of versionIds) query.append("version", id);
    if (group !== null) query.set("group", group);
    if (grouping !== null) query.set("grouping", grouping);
    return `/runs?${query.toString()}`;
}
