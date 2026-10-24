// "Show archived" kept in the address, the same way for every list: `archived=1` (a list that shares its page with another has a
// parameter of its own, e.g. `archivedGroups=1`). Absent means off; anything but `1` is read as off.

/** The parameter of the runs list; the groups list on the same page uses `archivedGroups`. */
export const ARCHIVED_RUNS_PARAM = "archived";
export const ARCHIVED_GROUPS_PARAM = "archivedGroups";

/** Whether `param` asks for archived items. */
export function archivedFromSearch(search: string, param: string): boolean {
    return new URLSearchParams(search).get(param) === "1";
}

/** The query with `param` set (`=1`) or removed; every other parameter stays. */
export function searchWithArchived(search: string, param: string, show: boolean): string {
    const query = new URLSearchParams(search);
    if (show) query.set(param, "1");
    else query.delete(param);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
