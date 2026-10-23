/** What a request gave, for one key. */
export interface Settled<T> {
    key: string;
    result: { kind: "error"; error: Error } | { kind: "loaded"; value: T };
}

/** `pending`: the result shown (if any) is for an earlier key, or nothing has arrived yet. */
export type Loaded<T> = ({ kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; value: T }) & { pending: boolean };

/** The result of a failed request: the fallback when there is one, else the error. */
export function settledWith<T>(failure: unknown, fallback: T | undefined): Settled<T>["result"] {
    if (fallback !== undefined) return { kind: "loaded", value: fallback };
    return { kind: "error", error: failure instanceof Error ? failure : new Error(String(failure)) };
}

/** What a hook shows for `key`: the last result (even for an earlier key), loading before the first, nothing asked for a null key. */
export function settledView<T>(settled: Settled<T> | null, key: string | null): Loaded<T> {
    if (key === null) return { kind: "loading", pending: false };
    if (settled === null) return { kind: "loading", pending: true };
    return { ...settled.result, pending: settled.key !== key };
}
