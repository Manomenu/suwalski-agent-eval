// Which versions the agent page lists by whether their runs are in groups, as the runs table does for runs: ordinary versions (the
// default), versions whose every run is in a group (`groupRunsOnly`), or all. A sibling of the runs table's groupFilter.ts rather than a
// generalisation: the address parameter is its own (`?versions=any|all`, no parameter means ordinary) and a version has no "one group by name".
export type VersionGrouping = "none" | "any" | "all";

const VERSIONS_PARAM = "versions";

/** The words of the runs' group filter, for versions. */
export const VERSION_GROUPING_OPTIONS: { value: VersionGrouping; label: string }[] = [
    { value: "none", label: "No group" },
    { value: "any", label: "Any group" },
    { value: "all", label: "All versions" },
];

/** The grouping an address asks for; anything unknown is the default. */
export function versionGroupingOf(search: string): VersionGrouping {
    const value = new URLSearchParams(search).get(VERSIONS_PARAM);
    return value === "any" || value === "all" ? value : "none";
}

/** The query with the parameter replaced; every other parameter stays. Empty when nothing is left. */
export function searchWithVersionGrouping(search: string, grouping: VersionGrouping): string {
    const query = new URLSearchParams(search);
    query.delete(VERSIONS_PARAM);
    if (grouping !== "none") query.set(VERSIONS_PARAM, grouping);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

/** The option value back to a grouping (the select's value is one of the three, or null). */
export function versionGroupingOfValue(value: string | null): VersionGrouping {
    return value === "any" || value === "all" ? value : "none";
}

/** Whether a version is listed under the grouping: ordinary for none, group runs only for any, every one for all. */
export function inVersionGrouping(version: { groupRunsOnly: boolean }, grouping: VersionGrouping): boolean {
    return grouping === "all" || version.groupRunsOnly === (grouping === "any");
}
