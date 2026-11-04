import { useEffect, useState } from "react";

import { settledView, settledWith, type Loaded, type Settled } from "./request";

export type { Loaded } from "./request";

export interface RequestOptions<T> {
    fallback?: T;
}

export function useRequest<T>(key: string | null, load: (signal: AbortSignal) => Promise<T>, options: RequestOptions<T> = {}): Loaded<T> {
    const [settled, setSettled] = useState<Settled<T> | null>(null);
    const { fallback } = options;

    useEffect(() => {
        if (key === null) return;
        // Why: StrictMode runs effects twice in development, so the first request is aborted and its result ignored
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
        // Why: key says what is asked for; load and fallback are new on every render, so they stay out of the dependencies
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key]);

    return settledView(settled, key);
}
