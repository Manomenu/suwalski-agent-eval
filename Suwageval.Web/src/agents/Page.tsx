import { Alert, Spinner } from "@maniumek/suwfrontkit";
import type { ReactNode } from "react";

import { isNotFound } from "../api/client";

export interface PageData<T> {
    isPending: boolean;
    error: Error | null;
    data: T | undefined;
}

export function Page<T>({ query, notFound, children }: { query: PageData<T>; notFound: string; children: (value: T) => ReactNode }) {
    if (query.error !== null) {
        const missing = isNotFound(query.error);
        return (
            <Alert variant="destructive">
                <p className="font-semibold">{missing ? "Not found" : "Could not load the page"}</p>
                <p>{missing ? notFound : query.error.message}</p>
            </Alert>
        );
    }
    if (query.isPending || query.data === undefined) return <Spinner label="Loading" />;
    return <>{children(query.data)}</>;
}
