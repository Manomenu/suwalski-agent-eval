import { useEffect, useEffectEvent, useState } from "react";

import { settledView, settledWith, type Loaded, type Settled } from "./request";

export type { Loaded } from "./request";

export interface RequestOptions<T> {
    fallback?: T;
}

export function useRequest<T>(key: string | null, load: (signal: AbortSignal) => Promise<T>, options: RequestOptions<T> = {}): Loaded<T> {
    const [settled, setSettled] = useState<Settled<T> | null>(null);
    const { fallback } = options;
    const loadLatest = useEffectEvent((signal: AbortSignal) => load(signal));
    const fallbackLatest = useEffectEvent(() => fallback);

    useEffect(() => {
        if (key === null) return;

        const controller = new AbortController();
        loadLatest(controller.signal)
            .then((value) => {
                if (!controller.signal.aborted) setSettled({ key, result: { kind: "loaded", value } });
            })
            .catch((e: unknown) => {
                if (!controller.signal.aborted) setSettled({ key, result: settledWith(e, fallbackLatest()) });
            });
        return () => {
            controller.abort();
        };
    }, [key]);

    return settledView(settled, key);
}
