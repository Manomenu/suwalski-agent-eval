// The rules of the Groups section: which groups are listed, and what a selection of them lets the buttons do (the same lifecycle as versions).

/** The groups to list: all of them with "Show archived" on, otherwise those in use. */
export function visibleGroups<T extends { archivedAt?: string | null }>(groups: T[], showArchived: boolean): T[] {
    return showArchived ? groups : groups.filter((group) => group.archivedAt == null);
}

/**
 * What the selected groups (by name) allow: the ones in use can be archived, the archived ones restored, and "Delete" acts on exactly one
 * selected group that is archived (only those can be deleted).
 */
export function groupSelection(
    groups: readonly { name: string; archivedAt?: string | null }[],
    selected: ReadonlySet<string>,
): { toArchive: string[]; toRestore: string[]; toDelete: string | null } {
    const chosen = groups.filter((group) => selected.has(group.name));
    const toRestore = chosen.filter((group) => group.archivedAt != null).map((group) => group.name);
    return {
        toArchive: chosen.filter((group) => group.archivedAt == null).map((group) => group.name),
        toRestore,
        toDelete: chosen.length === 1 ? (toRestore[0] ?? null) : null,
    };
}

export const GROUPS_PAGE_SIZE = 10;

const GROUPS_PAGE_PARAM = "groupsPage";

/** The groups page an address asks for (1-based); a missing or malformed value is the first page. */
export function groupsPageOf(search: string): number {
    const page = Number(new URLSearchParams(search).get(GROUPS_PAGE_PARAM));
    return Number.isInteger(page) && page > 1 ? page : 1;
}

/** The query with the groups page replaced; every other parameter stays. The first page leaves no parameter. */
export function searchWithGroupsPage(search: string, page: number): string {
    const query = new URLSearchParams(search);
    if (page > 1) query.set(GROUPS_PAGE_PARAM, String(page));
    else query.delete(GROUPS_PAGE_PARAM);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

/** One page of the groups, in their order; a page past the last (after groups were deleted or hidden) shows the last one. */
export function pageOfGroups<T>(groups: readonly T[], requestedPage: number): { groups: T[]; page: number; pageCount: number } {
    const pageCount = Math.max(1, Math.ceil(groups.length / GROUPS_PAGE_SIZE));
    const page = Math.min(requestedPage, pageCount);
    return { groups: groups.slice((page - 1) * GROUPS_PAGE_SIZE, page * GROUPS_PAGE_SIZE), page, pageCount };
}
