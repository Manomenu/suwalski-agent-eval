export interface Archivable {
    archivedAt?: string | null;
}

export function visibleItems<T extends Archivable>(items: T[], showArchived: boolean): T[] {
    return showArchived ? items : items.filter((item) => item.archivedAt == null);
}

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
