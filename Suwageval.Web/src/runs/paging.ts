// How the lists page: the size of a page and the query that asks the server for one.

/** How many runs one request fetches, and how many versions one "Show more" reveals. */
export const PAGE_SIZE = 20;

/**
 * The path of one page of an agent's runs, newest first: `before` is the id the previous page ended with, `versionIds` limits the
 * runs to those versions (none: all of them), `group` to the runs started in that group, `grouping` to the runs in no group (`None`)
 * or in some group (`Any`); none of them: all runs.
 */
export function runsPath(
    agent: string,
    before: string | null,
    versionIds: readonly string[],
    group: string | null = null,
    grouping: "None" | "Any" | null = null,
): string {
    const query = new URLSearchParams({ agent, limit: String(PAGE_SIZE) });
    if (before !== null) query.set("before", before);
    for (const id of versionIds) query.append("version", id);
    if (group !== null) query.set("group", group);
    if (grouping !== null) query.set("grouping", grouping);
    return `/runs?${query.toString()}`;
}
