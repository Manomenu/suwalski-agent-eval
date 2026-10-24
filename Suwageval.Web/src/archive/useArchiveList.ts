import { useState } from "react";

/**
 * The state versions and run groups share: the ticked rows (by key), a change in flight, its error, the notice of a deletion, and
 * whether the delete dialog is open. `archive` asks the server; `onChanged` makes the page load its lists again.
 */
export function useArchiveList(archive: (keys: string[], archived: boolean) => Promise<void>, onChanged: () => void) {
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);

    const clear = () => {
        setSelected(new Set());
    };

    const toggle = (key: string, checked: boolean) => {
        const next = new Set(selected);
        if (checked) next.add(key);
        else next.delete(key);
        setSelected(next);
    };

    const change = async (keys: string[], archived: boolean) => {
        setBusy(true);
        setError(null);
        setNotice(null);
        try {
            await archive(keys, archived);
            clear();
            onChanged();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : String(e));
        } finally {
            setBusy(false);
        }
    };

    /** The server deleted the row: close the dialog, say so and load again. */
    const deleted = (message: string) => {
        setDeleting(false);
        clear();
        setNotice(message);
        onChanged();
    };

    return { selected, busy, error, notice, deleting, setDeleting, clear, toggle, change, deleted };
}
