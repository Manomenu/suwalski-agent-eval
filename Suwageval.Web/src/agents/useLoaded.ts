import { useEffect, useState } from "react";

export type Loaded<T> = { kind: "loading" } | { kind: "error"; error: Error } | { kind: "loaded"; value: T };

/** What `load` returns, once; asked again when `key` changes. */
export function useLoaded<T>(key: string, load: (signal: AbortSignal) => Promise<T>): Loaded<T> {
    const [state, setState] = useState<Loaded<T>>({ kind: "loading" });

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        load(controller.signal)
            .then((value) => {
                if (!controller.signal.aborted) setState({ kind: "loaded", value });
            })
            .catch((e: unknown) => {
                if (!controller.signal.aborted) setState({ kind: "error", error: e instanceof Error ? e : new Error(String(e)) });
            });
        return () => {
            controller.abort();
        };
        // `load` is a new closure on every render; `key` says what it asks for.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key]);

    return state;
}
