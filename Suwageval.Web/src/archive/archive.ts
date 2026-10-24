// The rules versions and run groups share: which rows are listed, and what a selection of them lets the buttons do.

export interface Archivable {
    archivedAt?: string | null;
}

/** The rows to list: all of them with "Show archived" on, otherwise those in use. */
export function visibleItems<T extends Archivable>(items: T[], showArchived: boolean): T[] {
    return showArchived ? items : items.filter((item) => item.archivedAt == null);
}

/**
 * What the selected rows (by key) allow: the ones in use can be archived, the archived ones restored, and "Delete" acts on exactly one
 * selected row that is archived (only those can be deleted).
 */
export function archiveSelection<T extends Archivable>(
    items: readonly T[],
    selected: ReadonlySet<string>,
    keyOf: (item: T) => string,
): { toArchive: string[]; toRestore: string[]; toDelete: T | null } {
    const chosen = items.filter((item) => selected.has(keyOf(item)));
    const archived = chosen.filter((item) => item.archivedAt != null);
    return {
        toArchive: chosen.filter((item) => item.archivedAt == null).map(keyOf),
        toRestore: archived.map(keyOf),
        toDelete: chosen.length === 1 ? (archived[0] ?? null) : null,
    };
}
