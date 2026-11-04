export interface Settled<T> {
    key: string;
    result: { kind: "error"; error: Error } | { kind: "loaded"; value: T };
}

export type Loaded<T> = ({ kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; value: T }) & { pending: boolean };

export function settledWith<T>(failure: unknown, fallback: T | undefined): Settled<T>["result"] {
    if (fallback !== undefined) return { kind: "loaded", value: fallback };
    return { kind: "error", error: failure instanceof Error ? failure : new Error(String(failure)) };
}

export function settledView<T>(settled: Settled<T> | null, key: string | null): Loaded<T> {
    if (key === null) return { kind: "loading", pending: false };
    if (settled === null) return { kind: "loading", pending: true };
    return { ...settled.result, pending: settled.key !== key };
}
