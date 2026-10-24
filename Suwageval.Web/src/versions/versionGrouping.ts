// Which versions the Evaluation page's filters offer by whether their runs are in groups, as the runs table does for runs: ordinary
// versions, versions whose every run is in a group (`groupRunsOnly`), or all. The older address parameter of the Versions page
// (`?versions=any|all`) is read here too, to be mapped to its usage (`versionUsage.ts`).
export type VersionGrouping = "none" | "any" | "all";

const VERSIONS_PARAM = "versions";

/** The grouping an address asks for; anything unknown is the default. */
export function versionGroupingOf(search: string): VersionGrouping {
    const value = new URLSearchParams(search).get(VERSIONS_PARAM);
    return value === "any" || value === "all" ? value : "none";
}

/** Whether a version is listed under the grouping: ordinary for none, group runs only for any, every one for all. */
export function inVersionGrouping(version: { groupRunsOnly: boolean }, grouping: VersionGrouping): boolean {
    return grouping === "all" || version.groupRunsOnly === (grouping === "any");
}
