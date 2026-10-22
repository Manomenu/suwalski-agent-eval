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
