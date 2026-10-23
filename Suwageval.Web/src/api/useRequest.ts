import { useEffect, useState } from "react";

import { settledView, settledWith, type Loaded, type Settled } from "./request";

export type { Loaded } from "./request";

export interface RequestOptions<T> {
    /** What a failed request gives instead of an error: a hint on a page is never a reason to break it. */
    fallback?: T;
}

/**
 * What `load` returns, asked once per `key` and again when `key` changes. A null `key` asks nothing (`pending` is false).
 * While a new key loads, the previous result stays and `pending` is true. `load` is a new closure on every render: `key` says what it asks for.
 */
export function useRequest<T>(key: string | null, load: (signal: AbortSignal) => Promise<T>, options: RequestOptions<T> = {}): Loaded<T> {
    const [settled, setSettled] = useState<Settled<T> | null>(null);
    const { fallback } = options;

    useEffect(() => {
        if (key === null) return;
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        load(controller.signal)
            .then((value) => {
                if (!controller.signal.aborted) setSettled({ key, result: { kind: "loaded", value } });
            })
            .catch((e: unknown) => {
                if (!controller.signal.aborted) setSettled({ key, result: settledWith(e, fallback) });
            });
        return () => {
            controller.abort();
        };
        // `key` says what is asked for; `load` and `fallback` are new on every render.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key]);

    return settledView(settled, key);
}
