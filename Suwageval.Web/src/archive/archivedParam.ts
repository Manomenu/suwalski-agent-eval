export const ARCHIVED_RUNS_PARAM = "archived";
export const ARCHIVED_GROUPS_PARAM = "archivedGroups";

export function archivedFromSearch(search: string, param: string): boolean {
    return new URLSearchParams(search).get(param) === "1";
}

export function searchWithArchived(search: string, param: string, show: boolean): string {
    const query = new URLSearchParams(search);
    if (show) query.set(param, "1");
    else query.delete(param);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
