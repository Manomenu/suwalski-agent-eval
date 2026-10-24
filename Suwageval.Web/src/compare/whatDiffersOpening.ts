// Whether "What differs" is open is a convenience kept in the browser's storage (AGENTS.md 5.2): read defensively, folded when
// nothing valid is stored.

const STORAGE_KEY = "suwageval.compare.whatDiffersOpen";

type KeyValueStorage = Pick<Storage, "getItem" | "setItem">;

/** The browser's storage, or null when it is blocked (the accessor itself can throw). */
function browserStorage(): KeyValueStorage | null {
    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

/** True when the person left "What differs" open last time; folded on the first view and whenever the storage fails. */
export function readWhatDiffersOpen(storage: KeyValueStorage | null = browserStorage()): boolean {
    try {
        return storage?.getItem(STORAGE_KEY) === "true";
    } catch {
        return false;
    }
}

/** Remembers the choice; a full or blocked storage only means it is not remembered. */
export function saveWhatDiffersOpen(open: boolean, storage: KeyValueStorage | null = browserStorage()): void {
    try {
        storage?.setItem(STORAGE_KEY, String(open));
    } catch {
        // A convenience only: nothing to tell the person.
    }
}

/** The header's text: how many rows the section holds once that is known (null while the contracts load). */
export function whatDiffersTitle(rows: number | null): string {
    return rows === null ? "What differs" : `What differs · ${String(rows)}`;
}
