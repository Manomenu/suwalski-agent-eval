const STORAGE_KEY = "suwageval.compare.whatDiffersOpen";

type KeyValueStorage = Pick<Storage, "getItem" | "setItem">;

function browserStorage(): KeyValueStorage | null {
    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

export function readWhatDiffersOpen(storage: KeyValueStorage | null = browserStorage()): boolean {
    try {
        return storage?.getItem(STORAGE_KEY) === "true";
    } catch {
        return false;
    }
}

export function saveWhatDiffersOpen(open: boolean, storage: KeyValueStorage | null = browserStorage()): void {
    try {
        storage?.setItem(STORAGE_KEY, String(open));
    } catch {
        // Why: a convenience only: nothing to tell the person
    }
}

export function whatDiffersTitle(rows: number | null): string {
    return rows === null ? "What differs" : `What differs · ${String(rows)}`;
}
